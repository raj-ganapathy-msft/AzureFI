using System.Web;
using System.Web.Optimization;

namespace AzureFaultInjection
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/js/jquery-2.2.4.min.js",
                        "~/Scripts/js/jquery.validate.min.js",
                        "~/Scripts/js/jquery.sumoselect.js",
            "~/Scripts/js/tooltip.js"
                ));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                 "~/Scripts/js/moment.js",
                      "~/Scripts/js/bootstrap-datetimepicker.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/updated/css/bootstrap.min.css",
                      "~/Content/updated/css/custom.css",
                "~/Content/updated/css/style.css",
                "~/Content/updated/css/sumoselect.css",
                "~/Content/updated/css/tooltip.css",
                "~/Content/updated/css/loader.css"));
        }
    }
}
