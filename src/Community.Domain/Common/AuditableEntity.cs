using Community.Domain.Events;

namespace Community.Domain.Common
{
    public abstract class AuditableEntity<T> : Entity<T>, IAuditableEntity
    {
        protected AuditableEntity() : base() { }

        protected AuditableEntity(T id) : base(id) { }

        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public Guid? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public abstract class AuditableAggregateRoot<T> : AuditableEntity<T>, IAggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected AuditableAggregateRoot() : base() { }

        protected AuditableAggregateRoot(T id) : base(id) { }

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
