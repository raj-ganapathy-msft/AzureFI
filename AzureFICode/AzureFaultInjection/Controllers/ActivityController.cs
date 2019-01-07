using System.Web.Mvc;

namespace AzureFaultInjection.Controllers
{
    public class ActivityController : Controller
    {
        // GET: Activity
        public ActionResult Index()
        {
            return View();
        }
    }
}