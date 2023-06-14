using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using BulkyWeb.Areas.Customer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public ShoppingCartViewModel ShoppingCartViewModel { get; set; }
        public CartController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            var shoppingCartList = _unitOfWork.ShoppingCart
                                        .GetAll(sc => sc.ApplicationUserId == userId, includeProperties: "Product")
                                        .ToList();

            shoppingCartList.ForEach(sc => sc.Price = GetPriceBasedOnQuantity(sc));

            ShoppingCartViewModel = new()
            {
                ShoppingCartList = shoppingCartList,
                OrderTotal = shoppingCartList.Aggregate(0.0, (total, sc) => total += sc.Price * sc.Count)
            };

            return View(ShoppingCartViewModel);
        }

        public IActionResult Summary()
        {
            return View();
        }

        public IActionResult AddToCart(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(c => c.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult RemoveFromCart(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(c => c.Id == cartId);

            if (cartFromDb.Count <= 1)
            {
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult RemoveItemFromCart(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(c => c.Id == cartId);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}
