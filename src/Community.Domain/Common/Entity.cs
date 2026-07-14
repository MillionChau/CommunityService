using Community.Domain.Events;

namespace Community.Domain.Common
{
    public abstract class Entity<T> : IEquatable<Entity<T>>
    {
        public T Id { get; set; } = default!;

        protected Entity() { }

        protected Entity(T id)
        {
            Id = id;
        }

        public bool Equals(Entity<T>? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<T>.Default.Equals(Id, other.Id);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Entity<T>);
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }

        public static bool operator ==(Entity<T>? left, Entity<T>? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Entity<T>? left, Entity<T>? right)
        {
            return !Equals(left, right);
        }
    }

    public abstract class AggregateRoot<T> : Entity<T>, IAggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected AggregateRoot() : base() { }

        protected AggregateRoot(T id) : base(id) { }

        public void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
