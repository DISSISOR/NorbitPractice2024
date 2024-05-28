namespace ProjectManager;

interface IRepository<TEntity, TId>
{
    void Add(TEntity entity);
    void Delete(TId id);

    TEntity GetById(TId id);
    IEnumerable<TEntity> GetAll();
}
