using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MessageService
{
    private readonly ConcurrentDictionary<string, List<Func<string, string, Task>>> _subscriptions = new();

    // 👇 Event to notify listeners
    public event Action OnUsersChanged;

    public void Subscribe(string username, Func<string, string, Task> subscriber)
    {
        _subscriptions.AddOrUpdate(username,
            new List<Func<string, string, Task>> { subscriber },
            (key, existingSubscribers) =>
            {
                existingSubscribers.Add(subscriber);
                return existingSubscribers;
            });

        NotifyUsersChanged();
    }

    public void Unsubscribe(string username, Func<string, string, Task> subscriber)
    {
        if (_subscriptions.TryGetValue(username, out var subscribers))
        {
            subscribers.Remove(subscriber);
            if (subscribers.Count == 0)
            {
                _subscriptions.TryRemove(username, out _);
            }

            NotifyUsersChanged();
        }
    }

    public async Task SendEvent(string recipient, string eventType, string message)
    {
        if (_subscriptions.TryGetValue(recipient, out var subscribers))
        {
            foreach (var subscriber in subscribers)
            {
                await subscriber(eventType, message);
            }
        }
    }

    public IEnumerable<string> GetActiveUsernames()
    {
        return _subscriptions.Keys;
    }

    private void NotifyUsersChanged() => OnUsersChanged?.Invoke();
}

