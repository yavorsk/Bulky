using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using BulkyWeb.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    //[Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            List<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return View(productList);
        }

        public IActionResult Upsert(int? id)
        {
            IEnumerable<SelectListItem> categoryList = _unitOfWork.Category.GetAll().Select(c => new SelectListItem(c.Name, c.Id.ToString()));
            
            ProductViewModel viewModel = new()
            {
                Product = new Product(),
                CategoryList = categoryList
            };

            if (id != null && id != 0)
            {
                Product? productFromDb = _unitOfWork.Product.Get(c => c.Id == id);
                if (productFromDb != null)
                {
                    viewModel.Product = productFromDb;
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult Upsert(ProductViewModel productModel, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine(wwwRootPath, @"images\Products", fileName);

                    if (!string.IsNullOrEmpty(productModel.Product.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, productModel.Product.ImageUrl.TrimStart('\\'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    using (var fileStream = new FileStream(productPath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    productModel.Product.ImageUrl = @"\images\Products\" + fileName;
                }

                if (productModel.Product.Id == 0)
                {
                    _unitOfWork.Product.Add(productModel.Product);
                }
                else
                {
                    _unitOfWork.Product.Update(productModel.Product);
                }

                _unitOfWork.Save();
                string upsertAction = productModel.Product.Id == 0 ? "created" : "updated";
                TempData["success"] = $"Product {upsertAction} successfully!";
                return RedirectToAction("Index");
            }

            productModel.CategoryList = _unitOfWork.Category.GetAll().Select(c => new SelectListItem(c.Name, c.Id.ToString()));
            return View(productModel);
        }

        //public IActionResult Edit(int? id)
        //{
        //    if (id == null || id == 0)
        //    {
        //        return NotFound();
        //    }

        //    Product? productFromDb = _unitOfWork.Product.Get(c => c.Id == id);

        //    if (productFromDb == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(productFromDb);
        //}

        //[HttpPost]
        //public IActionResult Edit(Product productModel)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        _unitOfWork.Product.Update(productModel);
        //        _unitOfWork.Save();
        //        TempData["success"] = "Product updated successfully!";
        //        return RedirectToAction("Index");
        //    }

        //    return View();
        //}

        //public IActionResult Delete(int? id)
        //{
        //    if (id == null || id == 0)
        //    {
        //        return NotFound();
        //    }

        //    Product? productFromDb = _unitOfWork.Product.Get(c => c.Id == id);

        //    if (productFromDb == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(productFromDb);
        //}

        [HttpPost]
        [ActionName("Delete")]
        public IActionResult DeletePOST(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }

            Product? productFromDb = _unitOfWork.Product.Get(c => c.Id == id);

            if (productFromDb == null)
            {
                return NotFound();
            }

            _unitOfWork.Product.Remove(productFromDb);
            _unitOfWork.Save();
            TempData["success"] = "Product deleted successfully!";
            return RedirectToAction("Index");
        }

        #region apicalls
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return Json(new { data = productList});
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var productToDelete = _unitOfWork.Product.Get(p => p.Id == id);

            if (productToDelete == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToDelete.ImageUrl.TrimStart('\\'));
            if (System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }

            _unitOfWork.Product.Remove(productToDelete);
            _unitOfWork.Save();

            return Json(new { success= true, message = "Product deleted successfully" });
        }
        #endregion
    }
}
