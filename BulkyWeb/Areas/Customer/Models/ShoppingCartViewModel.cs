using Bulky.Models;

namespace BulkyWeb.Areas.Customer.Models
{
    public class ShoppingCartViewModel
    {
        public IEnumerable<ShoppingCart> ShoppingCartList { get; set; }
        public double OrderTotal { get; set; }
    }
}
