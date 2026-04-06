public class PickListHelper<TItem, TKey>
{
    private bool _isDirty;

    public Func<TItem, TKey> KeySelector { get; }
    public string TextProperty { get; }
    public Action Changed { get; }

    public List<TItem> AllAllowed { get; }
    public HashSet<TKey> OriginalSelectedKeys { get; private set; }
    public HashSet<TKey> CurrentSelectedKeys { get; private set; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            _isDirty = value;
            Changed?.Invoke();
        }
    }

    public PickListHelper(
        IEnumerable<TItem> allAllowed,
        IEnumerable<TKey> originalSelectedKeys,
        Func<TItem, TKey> keySelector,
        string textProperty,
        Action changed = null)
    {
        TextProperty = textProperty;
        Changed = changed;
        KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

        AllAllowed = allAllowed?.ToList() ?? new List<TItem>();

        OriginalSelectedKeys = new HashSet<TKey>(
            originalSelectedKeys ?? Enumerable.Empty<TKey>());

        CurrentSelectedKeys = new HashSet<TKey>(OriginalSelectedKeys);
        _isDirty = false;
    }

    public List<TMapped> GetAddedItems<TMapped>(Func<TItem, TMapped> mapper)
    {
        return AllAllowed
            .Where(item =>
                CurrentSelectedKeys.Contains(KeySelector(item)) &&
                !OriginalSelectedKeys.Contains(KeySelector(item)))
            .Select(mapper)
            .ToList();
    }

    public List<TCollectionItem> GetRemovedItems<TCollectionItem>(
        IEnumerable<TCollectionItem> collection,
        Func<TCollectionItem, TKey> keySelector)
    {
        return collection
            .Where(item =>
                OriginalSelectedKeys.Contains(keySelector(item)) &&
                !CurrentSelectedKeys.Contains(keySelector(item)))
            .ToList();
    }

    public void SetCurrentSelectedKeys(IEnumerable<TKey> keys)
    {
        CurrentSelectedKeys = new HashSet<TKey>(
            keys ?? Enumerable.Empty<TKey>());

        IsDirty = !OriginalSelectedKeys.SetEquals(CurrentSelectedKeys);
    }

    public void AcceptChanges()
    {
        OriginalSelectedKeys = new HashSet<TKey>(CurrentSelectedKeys);
        IsDirty = false;
    }

    public void Reset()
    {
        CurrentSelectedKeys = new HashSet<TKey>(OriginalSelectedKeys);
        IsDirty = false;
    }
}