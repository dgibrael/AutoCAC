using System.DirectoryServices;
using System.Runtime.Versioning;
using AutoCAC.Models;

namespace AutoCAC.Services;

[SupportedOSPlatform("windows")]
public interface IAdSearch
{
    UsersQuery Users();
}

[SupportedOSPlatform("windows")]
public sealed class AdSearchService : IAdSearch
{
    // ctor does nothing (safe in design-time/non-Windows if not registered)
    public AdSearchService() { }

    public UsersQuery Users()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Active Directory queries require Windows.");

        // Resolve the default naming context lazily
        using var rootDse = new DirectoryEntry("LDAP://RootDSE");
        var defaultNc = (string)rootDse.Properties["defaultNamingContext"][0];

        // Pass the NC to the query; it will create/Dispose DirectoryEntry as needed
        return new UsersQuery(defaultNc);
    }
}

[SupportedOSPlatform("windows")]
public sealed class UsersQuery
{
    private const string BaseUserClass = "(objectClass=user)";
    private readonly string _defaultNc;
    private readonly List<string> _and = new() { BaseUserClass };
    private readonly List<string> _props = new();
    private int _pageSize = 1000;
    private int _sizeLimit = 0; // 0 = no limit
    private TimeSpan _serverTimeLimit = TimeSpan.FromSeconds(30);
    private readonly List<string> _groupFilters = new(); // DN filters
    private string _sortAttr;
    private bool _sortDesc;
    public bool HasFilters =>
        _groupFilters.Count > 0 ||
        _and.Any(c => !string.Equals(c, BaseUserClass, StringComparison.Ordinal));
    public string LdapFilter => BuildFilter();
    internal UsersQuery(string defaultNamingContext) => _defaultNc = defaultNamingContext;

    public UsersQuery EnabledOnly(bool enabledOnly = true)
    {
        if (enabledOnly)
            _and.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))"); // not disabled
        return this;
    }

    public UsersQuery OrderByAttribute(string attribute, bool descending = false)
    {
        if (!string.IsNullOrWhiteSpace(attribute))
        {
            _sortAttr = attribute;
            _sortDesc = descending;
        }
        return this;
    }

    public UsersQuery UpnSuffix(string suffix)
    {
        if (!string.IsNullOrWhiteSpace(suffix))
            _and.Add($"(userPrincipalName=*{Ldap.Escape(suffix)})");
        return this;
    }

    public UsersQuery DisplayNameContains(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return this;
        var f = Ldap.Escape(fragment); // RFC4515
        _and.Add($"(displayName=*{f}*)");
        return this;
    }

    public UsersQuery UserNameContains(string fragment) // sAMAccountName
    {
        if (string.IsNullOrWhiteSpace(fragment)) return this;
        var f = Ldap.Escape(fragment); // RFC4515
        _and.Add($"(sAMAccountName=*{f}*)");
        return this;
    }

    public UsersQuery UserNameEquals(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return this;

        // Strip DOMAIN\ prefix if provided
        var value = userName.Contains('\\')
            ? userName.Split('\\', 2)[1]
            : userName;

        var f = Ldap.Escape(value.Trim());

        _and.Add($"(sAMAccountName={f})"); // exact match (case-insensitive)
        return this;
    }

    public UsersQuery UserContains(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return this;
        var f = Ldap.Escape(fragment);
        _and.Add($"(|(cn=*{f}*)(displayName=*{f}*)(givenName=*{f}*)(sn=*{f}*)(sAMAccountName=*{f}*)(userPrincipalName=*{f}*))");
        return this;
    }

    /// <summary>Users that (recursively) are members of a group identified by DN.</summary>
    public UsersQuery InGroupDn(string groupDn, bool recursive = true)
    {
        if (string.IsNullOrWhiteSpace(groupDn)) return this;

        // Use RFC4515 filter escaping for values embedded in filters
        var dnForFilter = Ldap.Escape(groupDn);

        _groupFilters.Add(recursive
            ? $"(memberOf:1.2.840.113556.1.4.1941:={dnForFilter})"
            : $"(memberOf={dnForFilter})");
        return this;
    }

    /// <summary>
    /// Accepts ANY identifier for equality: DN, DOMAIN\sam, samAccountName, CN, Name, DisplayName.
    /// Resolves to DN and applies a memberOf filter (optionally recursive).
    /// </summary>
    public UsersQuery InGroupEquals(string id, bool recursive = true)
    {
        var dn = ResolveGroupDnByAny(id);
        if (string.IsNullOrWhiteSpace(dn))
        {
            // Force an empty result set if the group can't be resolved
            _and.Add("(!(objectClass=*))");
            return this;
        }
        return InGroupDn(dn!, recursive);
    }

    /// <summary>
    /// Accepts ANY fragment (DN fragment, DOMAIN\sam, sam, CN, Name, DisplayName) and
    /// ORs all matching groups' DNs into memberOf filters. If none found -> empty result set.
    /// </summary>
    public UsersQuery InGroupContains(string inputFragment, bool recursive = true)
    {
        var dns = ResolveGroupDnsByNameContains(inputFragment).ToList();
        if (dns.Count == 0)
        {
            _and.Add("(!(objectClass=*))"); // no matching groups -> empty results
            return this;
        }

        foreach (var dn in dns)
            InGroupDn(dn, recursive);

        return this;
    }

    public UsersQuery Properties(params string[] attributes)
    {
        _props.Clear();
        _props.AddRange(attributes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return this;
    }

    public UsersQuery PageSize(int pageSize) { _pageSize = Math.Clamp(pageSize, 1, 1000); return this; }
    public UsersQuery SizeLimit(int sizeLimit) { _sizeLimit = Math.Max(0, sizeLimit); return this; }
    public UsersQuery ServerTimeLimit(TimeSpan t) { _serverTimeLimit = t; return this; }

    public UsersQuery SelectBasic()
        => Properties("sAMAccountName", "userPrincipalName", "displayName", "userAccountControl", "mail");

    public Task<List<AdUserDto>> ToListAsync(CancellationToken ct = default)
        => Task.Run(Execute, ct);

    // ----------------- internals -----------------

    private List<AdUserDto> Execute()
    {
        using var root = new DirectoryEntry($"LDAP://{_defaultNc}");
        using var searcher = new DirectorySearcher(root)
        {
            PageSize = _pageSize,
            SizeLimit = _sizeLimit,
            ServerTimeLimit = _serverTimeLimit,
            Filter = BuildFilter()
        };


        if (!string.IsNullOrEmpty(_sortAttr))
        {
            searcher.Sort = new SortOption(
                _sortAttr,
                _sortDesc ? SortDirection.Descending : SortDirection.Ascending
            );
        }

        if (_props.Count == 0) SelectBasic();
        foreach (var p in _props) searcher.PropertiesToLoad.Add(p);

        using var results = searcher.FindAll();
        var list = new List<AdUserDto>(results.Count);

        foreach (SearchResult r in results)
        {
            string Get(string name) => r.Properties[name] is { Count: > 0 } coll ? (string)coll[0] : string.Empty;
            bool enabled = r.Properties["userAccountControl"] is { Count: > 0 } uac
                ? (((int)uac[0]) & 0x2) == 0
                : true;
            list.Add(new AdUserDto(
                Get("sAMAccountName"),
                Get("userPrincipalName"),
                Get("displayName"),
                enabled,
                Get("mail")
                ));
        }

        return list
            .GroupBy(u => string.IsNullOrEmpty(u.UserPrincipalName) ? u.SamAccountName : u.UserPrincipalName,
                     StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private string BuildFilter()
    {
        var clauses = new List<string>(_and);
        if (_groupFilters.Count == 1) clauses.Add(_groupFilters[0]);
        else if (_groupFilters.Count > 1)
            clauses.Add($"(|{string.Join(string.Empty, _groupFilters)})"); // any group

        return $"(&{string.Join(string.Empty, clauses)})";
    }

    /// <summary>
    /// Resolve a single group DN from any identifier:
    /// - DN: return as-is
    /// - DOMAIN\sam: use the part after '\'
    /// - otherwise try sAMAccountName, cn, name, displayName (exact match)
    /// </summary>
    private string ResolveGroupDnByAny(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // If caller gave us a DN, trust it
        if (LooksLikeDn(input)) return input;

        // If DOMAIN\name provided, strip DOMAIN\
        var value = input.Contains('\\') ? input.Split('\\', 2)[1] : input;

        var v = Ldap.Escape(value); // RFC4515 filter escaping
        using var root = new DirectoryEntry($"LDAP://{_defaultNc}");
        using var ds = new DirectorySearcher(root)
        {
            Filter = $"(&(objectClass=group)(|(sAMAccountName={v})(cn={v})(name={v})(displayName={v})))",
            PageSize = 1000,
            SizeLimit = 1
        };
        ds.PropertiesToLoad.Add("distinguishedName");
        var sr = ds.FindOne();
        return sr?.Properties["distinguishedName"] is { Count: > 0 } dn ? (string)dn[0] : null;
    }

    /// <summary>
    /// Resolve multiple group DNs by a fragment across common name-like attributes.
    /// Accepts DN fragments, DOMAIN\sam, sam, CN/Name/DisplayName fragments.
    /// </summary>
    private IEnumerable<string> ResolveGroupDnsByNameContains(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) yield break;

        // If DOMAIN\sam, search by the sam fragment
        var normalized = fragment.Contains('\\') ? fragment.Split('\\', 2)[1] : fragment;
        var f = Ldap.Escape(normalized); // RFC4515 filter escaping

        using var root = new DirectoryEntry($"LDAP://{_defaultNc}");
        using var ds = new DirectorySearcher(root)
        {
            // Match groups where any of these attributes contains the fragment
            Filter = $"(&(objectClass=group)(|(cn=*{f}*)(name=*{f}*)(displayName=*{f}*)(sAMAccountName=*{f}*)))",
            PageSize = 1000,
            SizeLimit = 500
        };
        ds.PropertiesToLoad.Add("distinguishedName");

        using var results = ds.FindAll();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SearchResult r in results)
        {
            if (r.Properties["distinguishedName"] is { Count: > 0 } dnObj)
            {
                var dn = (string)dnObj[0];
                if (seen.Add(dn))
                    yield return dn; // feed into InGroupDn (which uses Ldap.Escape for filter values)
            }
        }
    }

    private static bool LooksLikeDn(string s) =>
        s.IndexOf("CN=", StringComparison.OrdinalIgnoreCase) >= 0 &&
        s.IndexOf("DC=", StringComparison.OrdinalIgnoreCase) >= 0 &&
        s.Contains(',');
}

internal static class Ldap
{
    // RFC 4515 filter escaping
    public static string Escape(string value) =>
        value.Replace(@"\", @"\5c")
             .Replace("*", @"\2a")
             .Replace("(", @"\28")
             .Replace(")", @"\29")
             .Replace("\0", @"\00");

    // Minimal DN escaping (RFC 4514) – not used in filters, but kept for completeness
    public static string EscapeDn(string dn) =>
        dn.Replace(@"\", @"\\").Replace(",", @"\,");
}
