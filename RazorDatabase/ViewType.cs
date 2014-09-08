using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace RazorDatabase
{    
    internal interface IInternalViewType
    {
        IEnumerable<WebViewPage> GetViews();
        object GetModel(WebViewPage webViewPage);
        void SetViewTypeName(string viewName);
        bool ShouldRender(WebViewPage webViewPage, IEnumerable<WebViewPage> webViewPages);
        void SetRenderedContent(string renderedContent);
        void MapProperties(WebViewPage webViewPage);
    }

    public interface IViewType
    {
    }

    /// <summary>
    /// Create derivatives of this class to indicate to RazorDatabase which view types
    /// should be included. Instances of this class are what RazorDatabase stores and queries.
    /// You should create any properties you want access to in your derived classes. Values for any
    /// properties will be mapped from the specified view. Several of the methods allow you to 
    /// configure how instances of the specified view type are treated by RazorDatabase.
    /// </summary>
    /// <typeparam name="TViewPage">The type of the view page to include in RazorDatabase.</typeparam>
    public abstract class ViewType<TViewPage> : IInternalViewType, IViewType
        where TViewPage : WebViewPage
    {
        /// <summary>
        /// This is the name of the type representing the view. Note that this is the name
        /// of the view type and not the view itself so any hyphens will have been 
        /// converted to underscores, etc.
        /// </summary>
        public string ViewTypeName { get; private set; }

        void IInternalViewType.SetViewTypeName(string viewName)
        {
            ViewTypeName = viewName;
        }

        /// <summary>
        /// Indicates whether a particular view page should be rendered. The default behavior is to render
        /// all views.
        /// </summary>
        /// <param name="viewPage">The view page to be rendered.</param>
        /// <param name="viewPages">All the view pages of type TViewPage. Use this to control rendering based on the
        /// collection of view pages (such as only render the most recent based on a date, etc.).</param>
        /// <returns>true to render the view page and store the result, false to not render the view page.</returns>
        protected bool ShouldRender(TViewPage viewPage, IEnumerable<TViewPage> viewPages)
        {
            return true;
        }

        bool IInternalViewType.ShouldRender(WebViewPage webViewPage, IEnumerable<WebViewPage> webViewPages)
        {
            return ShouldRender((TViewPage)webViewPage, webViewPages.Cast<TViewPage>());
        }

        /// <summary>
        /// This contains the rendered content of the view if this view was rendered at
        /// initialization.
        /// </summary>
        public string RenderedContent { get; private set; }

        void IInternalViewType.SetRenderedContent(string renderedContent)
        {
            RenderedContent = renderedContent;
        }

        /// <summary>
        /// This allows you to override the selection of views to include in the collection.
        /// The default behavior is to use reflection to get all views that can be assigned to
        /// TViewType in all loaded assemblies.
        /// </summary>
        /// <returns>All of the concrete views to include in the collection.</returns>
        protected virtual IEnumerable<TViewPage> GetViews()
        {
            List<TViewPage> views = new List<TViewPage>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type viewType in assembly.GetTypes().Where(x => typeof(TViewPage).IsAssignableFrom(x) && GetView(x)))
                    {
                        views.Add((TViewPage)Activator.CreateInstance(viewType));
                    }
                }
                catch (Exception)
                {
                    // Ignore if we can't get a particular type or assembly
                }
            }
            return views;
        }

        IEnumerable<WebViewPage> IInternalViewType.GetViews()
        {
            return GetViews().Cast<WebViewPage>();
        }

        /// <summary>
        ///  Override this to specify on a per-view basis which views should be included for this view type.
        /// </summary>
        /// <param name="x">The type of the view.</param>
        /// <returns>true if the view should be included, false if not.</returns>
        protected virtual bool GetView(Type x)
        {
            return true;
        }

        // This returns a null model by default
        /// <summary>
        /// This allows you to supply a model to the view when rendering during RazorDatabase initialization
        /// (since no controller/action is involved in this process). The default behavior is to supply a null model.
        /// </summary>
        /// <param name="webViewPage">The view being rendered.</param>
        /// <returns>The model to supply to the view during rendering.</returns>
        protected virtual object GetModel(TViewPage webViewPage)
        {
            return null;
        }

        object IInternalViewType.GetModel(WebViewPage webViewPage)
        {
            return GetModel((TViewPage)webViewPage);
        }

        // This attempts to fill public properties by first looking for matching property names and types in the view and then checking the ViewBag for matching keys
        /// <summary>
        /// This method fills the properties of this ViewType with values from the source view. The default implementation first checks the view itself for matching property names
        /// and then checks the ViewData and ViewBag (which will both be empty if the view wasn't rendered). Override this method to provide your own mapping logic.
        /// </summary>
        /// <param name="webViewPage">The actual view.</param>
        protected virtual void MapProperties(TViewPage webViewPage)
        {
            foreach (PropertyInfo destinationProp in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanWrite && x.Name != "Rendered"))
            {
                // Check the view page for a matching property
                PropertyInfo sourceProp = typeof(TViewPage).GetProperty(destinationProp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (sourceProp != null && sourceProp.CanRead && destinationProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    destinationProp.SetValue(this, sourceProp.GetValue(webViewPage));
                    continue;
                }

                // Try ViewData
                object sourceValue;
                if (webViewPage.ViewData.TryGetValue(destinationProp.Name, out sourceValue) && sourceValue != null && destinationProp.PropertyType.IsAssignableFrom(sourceValue.GetType()))
                {
                    destinationProp.SetValue(this, sourceValue);
                    continue;
                }

                // Try ViewBag (MVC uses an ExpandoObject for ViewBag, so we should be able to cast it to IDictionary)
                IDictionary<string, object> viewBag = webViewPage.ViewBag as IDictionary<string, object>;
                if (viewBag != null && viewBag.TryGetValue(destinationProp.Name, out sourceValue) && sourceValue != null && destinationProp.PropertyType.IsAssignableFrom(sourceValue.GetType()))
                {
                    destinationProp.SetValue(this, sourceValue);
                    continue;
                }
            }
        }

        void IInternalViewType.MapProperties(WebViewPage webViewPage)
        {
            MapProperties((TViewPage)webViewPage);
        }
    }
}
