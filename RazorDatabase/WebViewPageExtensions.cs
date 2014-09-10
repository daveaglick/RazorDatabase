using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.WebPages;
using System.Reflection;

namespace RazorDatabase
{
    // This is essentially from RazorGenerator.Testing
    internal static class WebViewPageExtensions
    {
        //private static DummyViewEngine _viewEngine = new DummyViewEngine();

        public static string Render(this WebViewPage view, object model = null)
        {
            return Render(view, null, model);
        }

        public static string Render(this WebViewPage view, HttpContextBase httpContext, object model = null)
        {
            StringWriter writer = new StringWriter();
            view.Initialize(httpContext, writer);
            view.ViewData.Model = model;
            WebPageContext webPageContext = new WebPageContext(view.ViewContext.HttpContext, null, model);

            // Using private reflection to access some internals
            // Also make sure the use the same writer used for initializing the ViewContext in the OutputStack
            // Note: ideally we would not have to do this, but WebPages is just not mockable enough :( 

            // Add the writer to the output stack
            PropertyInfo outputStackProp = typeof(WebPageContext).GetProperty("OutputStack", BindingFlags.Instance | BindingFlags.NonPublic);
            Stack<TextWriter> outputStack = (Stack<TextWriter>)outputStackProp.GetValue(webPageContext, null);
            outputStack.Push(writer);

            // Push some section writer dictionary onto the stack. We need two, because the logic in WebPageBase.RenderBody
            // checks that as a way to make sure the layout page is not called directly
            PropertyInfo sectionWritersStackProp = typeof(WebPageContext).GetProperty("SectionWritersStack", BindingFlags.Instance | BindingFlags.NonPublic);
            Stack<Dictionary<string, SectionWriter>> sectionWritersStack = (Stack<Dictionary<string, SectionWriter>>)sectionWritersStackProp.GetValue(webPageContext, null);
            Dictionary<string, SectionWriter> sectionWriters = new Dictionary<string, SectionWriter>(StringComparer.OrdinalIgnoreCase);
            sectionWritersStack.Push(sectionWriters);
            sectionWritersStack.Push(sectionWriters);

            // Set the body delegate to do nothing
            PropertyInfo bodyActionProp = typeof(WebPageContext).GetProperty("BodyAction", BindingFlags.Instance | BindingFlags.NonPublic);
            bodyActionProp.SetValue(webPageContext, (Action<TextWriter>)(w => { }), null);

            // Set the page context on the view (the property is public, but the setter is internal)
            PropertyInfo pageContextProp = typeof(WebPageRenderingBase).GetProperty("PageContext", BindingFlags.Instance | BindingFlags.Public);
            pageContextProp.SetValue(view, webPageContext, BindingFlags.NonPublic, null, null, null);

            // Execute/render the view
            view.Execute();

            return writer.ToString();
        }

        private static void Initialize(this WebViewPage view, HttpContextBase httpContext, TextWriter writer)
        {
            //EnsureViewEngineRegistered();
            HttpContextBase context = httpContext ?? new DummyHttpContext();
            RouteData routeData = new RouteData();
            ControllerContext controllerContext = new ControllerContext(context, routeData, new DummyController());
            view.ViewContext = new ViewContext(controllerContext, new DummyView(), view.ViewData, new TempDataDictionary(), writer);
            view.InitHelpers();
        }
            
        //private static void EnsureViewEngineRegistered()
        //{
        //    // Make sure our dummy view engine is registered
        //    lock (_viewEngine)
        //    {
        //        if (!ViewEngines.Engines.Contains(_viewEngine))
        //        {
        //            ViewEngines.Engines.Clear();
        //            ViewEngines.Engines.Insert(0, _viewEngine);
        //        }
        //    }
        //}

        // Currently unused
        private class DummyViewEngine : IViewEngine
        {
            public ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
            {
                return new ViewEngineResult(new DummyView { ViewName = partialViewName }, this);
            }

            public ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
            {
                return new ViewEngineResult(new DummyView { ViewName = viewName }, this);
            }

            public void ReleaseView(ControllerContext controllerContext, IView view)
            {
            }
        }

        // Currently unused
        private class DummyView : IView
        {
            public string ViewName { get; set; }

            public void Render(ViewContext viewContext, TextWriter writer)
            {
                // Render a marker instead of actually rendering the partial view
                writer.WriteLine(String.Format("/* {0} */", ViewName));
            }
        }

        private class DummyController : ControllerBase
        {
            protected override void ExecuteCore() { }
        }


        private class DummyHttpRequest : HttpRequestBase
        {
            public override bool IsLocal
            {
                get { return false; }
            }

            public override string ApplicationPath
            {
                get { return "/"; }
            }

            public override NameValueCollection ServerVariables
            {
                get { return new NameValueCollection(); }
            }

            public override string RawUrl
            {
                get { return ""; }
            }
        }

        private class DummyHttpResponse : HttpResponseBase
        {
            public override string ApplyAppPathModifier(string virtualPath)
            {
                return virtualPath;
            }
        }

        private class DummyHttpContext : HttpContextBase
        {
            private HttpRequestBase _request = new DummyHttpRequest();
            private HttpResponseBase _response = new DummyHttpResponse();
            private IDictionary _items = new Hashtable();

            public override HttpRequestBase Request
            {
                get { return _request; }
            }

            public override HttpResponseBase Response
            {
                get { return _response; }
            }

            public override IDictionary Items
            {
                get { return _items; }
            }
        }
    }
}
