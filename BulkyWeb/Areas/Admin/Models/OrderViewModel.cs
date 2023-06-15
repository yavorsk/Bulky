using Bulky.Models;

namespace BulkyWeb.Areas.Admin.Models
{
	public class OrderViewModel
	{
		public OrderHeader OrderHeader { get; set; }
		public IEnumerable<OrderDetail>	OrderDetails { get; set; }
	}
}
