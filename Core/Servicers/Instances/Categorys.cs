using Core.Librarys.SQLite;
using Core.Models;
using Core.Servicers.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    public class Categorys : ICategorys
    {
        private List<CategoryModel> _categories;
        public Categorys()
        {
            this._categories = new List<CategoryModel>();
        }

        public async Task<CategoryModel> CreateAsync(CategoryModel category)
        {
            using var db = new TaiDbContext();
            db.Categorys.Add(category);
            await db.SaveChangesAsync();
            _categories.Add(category);
            return category;
        }

        public async Task DeleteAsync(CategoryModel category)
        {
            using var db = new TaiDbContext();

            var item = await db.Categorys.FirstOrDefaultAsync(m => m.ID == category.ID);
            if (item != null)
            {
                db.Categorys.Remove(item);
                await db.SaveChangesAsync();
                _categories.Remove(category);
            }
        }

        public List<CategoryModel> GetCategories()
        {
            return this._categories;
        }

        public CategoryModel GetCategory(int id)
        {
            return _categories.Where(m => m.ID == id).FirstOrDefault();
        }

        public async Task LoadAsync()
        {
            Debug.WriteLine("加载分类");
            using var db = new TaiDbContext();
            this._categories = await db.Categorys.ToListAsync();
            Debug.WriteLine("加载分类完成");

        }


        public async Task UpdateAsync(CategoryModel category)
        {
            using var db = new TaiDbContext();
            db.Categorys.Update(category);
            await db.SaveChangesAsync();
        }
    }
}
