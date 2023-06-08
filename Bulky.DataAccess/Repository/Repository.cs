using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Bulky.DataAccess.Repository
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext _db;
        protected DbSet<T> dbSet;

        public Repository(ApplicationDbContext db)
        {
            this._db = db;
            this.dbSet = db.Set<T>();
        }

        public void Add(T entity)
        {
            this.dbSet.Add(entity);
        }

        public T Get(Expression<Func<T, bool>> filter)
        {
            IQueryable<T> query = this.dbSet;
            query = query.Where(filter);
            return query.FirstOrDefault();
        }

        public IEnumerable<T> GetAll()
        {
            IQueryable<T> query = this.dbSet;
            return query.ToList();
        }

        public void Remove(T entity)
        {
            dbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            dbSet.RemoveRange(entities);
        }
    }
}
