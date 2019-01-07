using System.Web.Mvc;

namespace AzureFaultInjection.Controllers
{
    public class ScheduleController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Reports Page";

            return View();
        }
    }
}
