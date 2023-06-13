using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    //[Authorize(Roles = SD.Role_Admin)]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            List<Company> CompanyList = _unitOfWork.Company.GetAll().ToList();
            return View(CompanyList);
        }

        public IActionResult Upsert(int? id)
        {
            var viewModel = new Company();

            if (id != null && id != 0)
            {
                Company? companyFromDb = _unitOfWork.Company.Get(c => c.Id == id);
                if (companyFromDb != null)
                {
                    viewModel = companyFromDb;
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult Upsert(Company companyModel)
        {
            if (ModelState.IsValid)
            {
                if (companyModel.Id == 0)
                {
                    _unitOfWork.Company.Add(companyModel);
                }
                else
                {
                    _unitOfWork.Company.Update(companyModel);
                }

                _unitOfWork.Save();
                string upsertAction = companyModel.Id == 0 ? "created" : "updated";
                TempData["success"] = $"Company {upsertAction} successfully!";
                return RedirectToAction("Index");
            }

            return View(companyModel);
        }

        [HttpPost]
        [ActionName("Delete")]
        public IActionResult DeletePOST(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }

            Company? CompanyFromDb = _unitOfWork.Company.Get(c => c.Id == id);

            if (CompanyFromDb == null)
            {
                return NotFound();
            }

            _unitOfWork.Company.Remove(CompanyFromDb);
            _unitOfWork.Save();
            TempData["success"] = "Company deleted successfully!";
            return RedirectToAction("Index");
        }

        #region apicalls
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Company> CompanyList = _unitOfWork.Company.GetAll().ToList();
            return Json(new { data = CompanyList});
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var CompanyToDelete = _unitOfWork.Company.Get(p => p.Id == id);

            if (CompanyToDelete == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            _unitOfWork.Company.Remove(CompanyToDelete);
            _unitOfWork.Save();

            return Json(new { success= true, message = "Company deleted successfully" });
        }
        #endregion
    }
}
