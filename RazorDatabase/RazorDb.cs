using System.Web.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using Newtonsoft.Json.Bson;
using System.Linq.Expressions;

namespace RazorDatabase
{
    /// <summary>
    /// This is the primary entry point for RazorDatabase.
    /// </summary>
    public static class RazorDb
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentBag<IInternalViewType>> _viewTypes 
            = new ConcurrentDictionary<Type,ConcurrentBag<IInternalViewType>>();

        /// <summary>
        /// This initializes RazorDatabase. Typically this involves finding ViewType classes, instantiating them,
        /// optionally rendering the corresponding views, mapping properties, and storing the results.
        /// </summary>
        /// <param name="cacheToDisk">Set to true to cacheToDisk the results of initialization to a file in App_data. This
        /// will speed up initialization on application restart.</param>
        public static void Initialize(bool cacheToDisk = true)
        {
            // Reflect over all loaded assemblies looking for ViewType objects
            HashSet<Type> viewTypes = new HashSet<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type viewType in assembly.GetTypes().Where(x => typeof(IInternalViewType).IsAssignableFrom(x) && !x.IsAbstract && !x.ContainsGenericParameters))
                    {
                        viewTypes.Add(viewType);
                    }
                }
                catch (Exception)
                {
                    // Ignore if we can't get a particular type or assembly
                }
            }

            // Get the App_Data folder and create a JSON serializer
            string appData = HostingEnvironment.MapPath("~/App_Data");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
            JsonSerializer serializer = new JsonSerializer();

            // Check for existing cached data if requested
            if (cacheToDisk)
            {
                foreach (Type viewType in viewTypes.ToArray())
                {
                    string fileName = Path.Combine(appData, "RazorDatabase." + viewType.FullName + ".rd");
                    if (File.Exists(fileName))
                    {
                        // There is an existing file, so attempt to deserialize it
                        try
                        {
                            using (Stream stream = new FileStream(fileName, FileMode.Open))
                            {
                                using (BinaryReader binaryReader = new BinaryReader(stream))
                                {
                                    // Make sure the cached version is the same as the current version
                                    int cachedAssemblyHash = binaryReader.ReadInt32();
                                    int assemblyHash = viewType.Assembly.GetHashCode();
                                    if (cachedAssemblyHash == assemblyHash)
                                    {
                                        // Need to set the readRootValueAsArray flag - see http://stackoverflow.com/questions/16910369/bson-array-deserialization-with-json-net
                                        using (JsonReader jsonReader = new BsonReader(binaryReader, true, DateTimeKind.Utc))
                                        {
                                            Array instanceArray = (Array)serializer.Deserialize(jsonReader, viewType.MakeArrayType());
                                            if (instanceArray != null)
                                            {
                                                ConcurrentBag<IInternalViewType> bag = new ConcurrentBag<IInternalViewType>(instanceArray.Cast<IInternalViewType>());
                                                _viewTypes.TryAdd(viewType, bag);
                                                viewTypes.Remove(viewType);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // TODO: Should probably log something here...
                        }
                    }
                }
            }

            // Render the remaining view types
            List<Tuple<Type, Array>> toSerialize = new List<Tuple<Type, Array>>();
            foreach (Type viewTypeType in viewTypes)
            {
                // Get a function for object instantiation
                Func<object> createViewType = Expression.Lambda<Func<object>>(
                    Expression.Convert(Expression.New(viewTypeType), typeof(object)))
                    .Compile();

                ConcurrentBag<IInternalViewType> bag = new ConcurrentBag<IInternalViewType>();
                IInternalViewType viewType = (IInternalViewType)createViewType();
                IEnumerable<WebViewPage> webViewPages = viewType.GetViews();
                foreach (WebViewPage webViewPage in webViewPages)
                {
                    viewType = (IInternalViewType)createViewType();
                    object model = viewType.GetModel(webViewPage);
                    viewType.SetViewTypeName(webViewPage.GetType().Name);
                    string rendered = viewType.ShouldRender(webViewPage, webViewPages) ? webViewPage.Render(model) : string.Empty;
                    viewType.SetRenderedContent(rendered);
                    viewType.MapProperties(webViewPage);
                    bag.Add(viewType);
                }
                if (bag.Count > 0)
                {
                    toSerialize.Add(new Tuple<Type, Array>(viewTypeType, bag.ToArray()));
                }
                _viewTypes.TryAdd(viewTypeType, bag);
            }

            // Serialize the view types that we rendered, if requested
            if (cacheToDisk)
            {
                foreach (Tuple<Type, Array> serialize in toSerialize)
                {
                    string fileName = Path.Combine(appData, "RazorDatabase." + serialize.Item1.FullName + ".rd");
                    using (Stream stream = new FileStream(fileName, FileMode.Create))
                    {
                        using (BinaryWriter binaryWriter = new BinaryWriter(stream))
                        {
                            binaryWriter.Write(serialize.Item1.Assembly.GetHashCode());
                            using (JsonWriter jsonWriter = new BsonWriter(binaryWriter))
                            {
                                serializer.Serialize(jsonWriter, serialize.Item2);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// This gets all of the ViewType instances of a given type.
        /// </summary>
        /// <typeparam name="TViewType">The actual ViewType to get.</typeparam>
        /// <returns>All the ViewType instances of the specified type.</returns>
        public static IEnumerable<TViewType> Get<TViewType>()
            where TViewType : IViewType
        {
            ConcurrentBag<IInternalViewType> bag;
            if (_viewTypes.TryGetValue(typeof(TViewType), out bag))
            {
                return bag.Cast<TViewType>();
            }
            return new TViewType[] { };
        }        
    }
}
