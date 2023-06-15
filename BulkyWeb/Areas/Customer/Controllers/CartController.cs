using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using BulkyWeb.Areas.Customer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;
using Newtonsoft.Json.Linq;
using Bulky.Utility;
using Stripe.Checkout;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace BulkyWeb.Areas.Customer.Controllers
{
	[Area("Customer")]
	[Authorize]
	public class CartController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		[BindProperty]
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
				OrderHeader = new() { OrderTotal = shoppingCartList.Aggregate(0.0, (total, sc) => total += sc.Price * sc.Count) }
			};

			return View(ShoppingCartViewModel);
		}

		public IActionResult Summary()
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
				OrderHeader = new()
				{
					OrderTotal = shoppingCartList.Aggregate(0.0, (total, sc) => total += sc.Price * sc.Count),
					ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId),
				}
			};

			ShoppingCartViewModel.OrderHeader.Name = ShoppingCartViewModel.OrderHeader.ApplicationUser.Name;
			ShoppingCartViewModel.OrderHeader.PhoneNumber = ShoppingCartViewModel.OrderHeader.ApplicationUser.PhoneNumber;
			ShoppingCartViewModel.OrderHeader.StreetAddress = ShoppingCartViewModel.OrderHeader.ApplicationUser.StreetAddress;
			ShoppingCartViewModel.OrderHeader.City = ShoppingCartViewModel.OrderHeader.ApplicationUser.City;
			ShoppingCartViewModel.OrderHeader.State = ShoppingCartViewModel.OrderHeader.ApplicationUser.State;
			ShoppingCartViewModel.OrderHeader.PostalCode = ShoppingCartViewModel.OrderHeader.ApplicationUser.PostalCode;

			ShoppingCartViewModel.OrderHeader = new();

			return View(ShoppingCartViewModel);
		}

		[HttpPost]
		[ActionName("Summary")]
		public IActionResult SummaryPOST()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

			var shoppingCartList = _unitOfWork.ShoppingCart
										.GetAll(sc => sc.ApplicationUserId == userId, includeProperties: "Product")
										.ToList();

			shoppingCartList.ForEach(sc => sc.Price = GetPriceBasedOnQuantity(sc));

			ShoppingCartViewModel.ShoppingCartList = shoppingCartList;
			ShoppingCartViewModel.OrderHeader.OrderDate = DateTime.Now;
			ShoppingCartViewModel.OrderHeader.ApplicationUserId = userId;
			ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
			ShoppingCartViewModel.OrderHeader.OrderTotal = shoppingCartList.Aggregate(0.0, (total, sc) => total += sc.Price * sc.Count);

			if (applicationUser.CompanyId.GetValueOrDefault() == 0)
			{
				//it is a regular customer 
				ShoppingCartViewModel.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
				ShoppingCartViewModel.OrderHeader.OrderStatus = SD.StatusPending;
			}
			else
			{
				//it is a company user
				ShoppingCartViewModel.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
				ShoppingCartViewModel.OrderHeader.OrderStatus = SD.StatusApproved;
			}

			_unitOfWork.OrderHeader.Add(ShoppingCartViewModel.OrderHeader);
			_unitOfWork.Save();

			foreach (var cart in ShoppingCartViewModel.ShoppingCartList)
			{
				OrderDetail orderDetail = new()
				{
					ProductId = cart.ProductId,
					OrderHeaderId = ShoppingCartViewModel.OrderHeader.Id,
					Price = cart.Price,
					Count = cart.Count
				};

				_unitOfWork.OrderDetail.Add(orderDetail);
				_unitOfWork.Save();
			}

			if (applicationUser.CompanyId.GetValueOrDefault() == 0)
			{
				// regular customer we need to capture payment
				//stripe logic
				var domain = "https://localhost:7169/";
				var options = new SessionCreateOptions
				{
					SuccessUrl = domain + $"customer/cart/OrderConfirmation?orderId={ShoppingCartViewModel.OrderHeader.Id}",
					CancelUrl = domain + "customer/cart/Index",
					LineItems = new List<SessionLineItemOptions>(),
					Mode = "payment",
				};

				foreach (var item in ShoppingCartViewModel.ShoppingCartList)
				{
					var sessionLineItem = new SessionLineItemOptions
					{
						PriceData = new SessionLineItemPriceDataOptions
						{
							UnitAmount = (long)(item.Price * 100), // $20.50 => 2050
							Currency = "usd",
							ProductData = new SessionLineItemPriceDataProductDataOptions
							{
								Name = item.Product.Title
							}
						},
						Quantity = item.Count
					};
					options.LineItems.Add(sessionLineItem);
				}

				var service = new SessionService();
				Session session = service.Create(options);

				_unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartViewModel.OrderHeader.Id, session.Id, session.PaymentIntentId);
				_unitOfWork.Save();
				Response.Headers.Add("Location", session.Url);
				return new StatusCodeResult(303);
			}

			return RedirectToAction(nameof(OrderConfirmation), new { orderId = ShoppingCartViewModel.OrderHeader.Id });
		}

		public IActionResult OrderConfirmation(int orderId)
		{
			OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includeProperties: "ApplicationUser");
			if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
			{
				//this is an order by customer
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);

				if (session.PaymentStatus.ToLower() == "paid")
				{
					_unitOfWork.OrderHeader.UpdateStripePaymentID(orderId, session.Id, session.PaymentIntentId);
					_unitOfWork.OrderHeader.UpdateStatus(orderId, SD.StatusApproved, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
				HttpContext.Session.Clear();
			}

			//_emailSender.SendEmailAsync(orderHeader.ApplicationUser.Email, "New Order - Bulky Book",
			//	$"<p>New Order Created - {orderHeader.Id}</p>");

			List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();

			_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
			_unitOfWork.Save();

			return View(orderId);
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
			var cartFromDb = _unitOfWork.ShoppingCart.Get(c => c.Id == cartId, tracked: true);

			if (cartFromDb.Count <= 1)
			{
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
					.GetAll(sc => sc.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
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
			var cartFromDb = _unitOfWork.ShoppingCart.Get(c => c.Id == cartId, tracked: true);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
				.GetAll(sc => sc.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
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
