using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;
using Sitecore.Data;
using Sitecore.Extensions.StringExtensions;
using Sitecore.Layouts;
using Sitecore.Mvc;
using Sitecore.Mvc.Pipelines.Response.RenderRendering;
using Sitecore.Mvc.Presentation;
using HtmlHelperExtensions = React.Web.Mvc.HtmlHelperExtensions;
using IReactComponent = React.IReactComponent;
using IReactEnvironment = React.IReactEnvironment;
using ReactEnvironment = React.ReactEnvironment;
using ReactNotInitialisedException = React.Exceptions.ReactNotInitialisedException;
using TinyIoCResolutionException = React.TinyIoC.TinyIoCResolutionException;
using Sitecore.React.Configuration;
using Sitecore.React.Cache;
using Sitecore.Rules.ConditionalRenderings;
using PageContext = Sitecore.Mvc.Presentation.PageContext;
using Sitecore.React.Util;

namespace Sitecore.React.Mvc
{
	/// <summary>Represents the class used to create views that have Razor syntax.</summary>
	public class JsxView : BuildManagerCompiledView
	{
        /// <summary>Gets the layout or master page.</summary>
        /// <returns>The layout or master page.</returns>
        public string LayoutPath { get; private set; }

		/// <summary>Gets a value that indicates whether view start files should be executed before the view.</summary>
		/// <returns>A value that indicates whether view start files should be executed before the view.</returns>
		public bool RunViewStartPages { get; private set; }

		internal IVirtualPathFactory VirtualPathFactory { get; set; }

		internal DisplayModeProvider DisplayModeProvider { get; set; }

		/// <summary>Gets or sets the set of file extensions that will be used when looking up view start files.</summary>
		/// <returns>The set of file extensions that will be used when looking up view start files.</returns>
		public IEnumerable<string> ViewStartFileExtensions { get; private set; }

        /// <summary>
        /// The bundle cache was added so that we can defer load async on scripts by wrapping all JSX client side code with a bootstrap method.
        /// </summary>
        private static CustomCacheService _bundleCache = new CustomCacheService();
        
        /// <summary>Initializes a new instance of the <see cref="T:System.Web.Mvc.RazorView" /> class.</summary>
        /// <param name="controllerContext">The controller context.</param>
        /// <param name="viewPath">The view path.</param>
        /// <param name="layoutPath">The layout or master page.</param>
        /// <param name="runViewStartPages">A value that indicates whether view start files should be executed before the view.</param>
        /// <param name="viewStartFileExtensions">The set of extensions that will be used when looking up view start files.</param>
        public JsxView(ControllerContext controllerContext, string viewPath, string layoutPath, bool runViewStartPages, IEnumerable<string> viewStartFileExtensions)
		  : this(controllerContext, viewPath, layoutPath, runViewStartPages, viewStartFileExtensions, null)
		{
		}

		/// <summary>Initializes a new instance of the <see cref="T:System.Web.Mvc.RazorView" /> class using the view page activator.</summary>
		/// <param name="controllerContext">The controller context.</param>
		/// <param name="viewPath">The view path.</param>
		/// <param name="layoutPath">The layout or master page.</param>
		/// <param name="runViewStartPages">A value that indicates whether view start files should be executed before the view.</param>
		/// <param name="viewStartFileExtensions">The set of extensions that will be used when looking up view start files.</param>
		/// <param name="viewPageActivator">The view page activator.</param>
		public JsxView(ControllerContext controllerContext, string viewPath, string layoutPath, bool runViewStartPages, IEnumerable<string> viewStartFileExtensions, IViewPageActivator viewPageActivator)
		  : base(controllerContext, viewPath, viewPageActivator)
		{
			this.LayoutPath = layoutPath ?? string.Empty;
			this.RunViewStartPages = runViewStartPages;
			this.ViewStartFileExtensions = viewStartFileExtensions ?? Enumerable.Empty<string>();
		}
        
		/// <summary>Renders the specified view context by using the specified the writer object.</summary>
		/// <param name="viewContext">Information related to rendering a view, such as view data, temporary data, and form context.</param>
		/// <param name="writer">The writer object.</param>
		/// <exception cref="T:System.ArgumentNullException">The <paramref name="viewContext" /> parameter is null.</exception>
		/// <exception cref="T:SInvalidOperationException">An instance of the view type could not be created.</exception>
		public override void Render(ViewContext viewContext, TextWriter writer)
		{
			if (viewContext == null)
			{
				throw new ArgumentNullException(nameof(viewContext));
			}

			this.RenderView(viewContext, writer, null);
		}

		/// <summary>Renders the specified view context by using the specified writer and <see cref="T:System.Web.Mvc.WebViewPage" /> instance.</summary>
		/// <param name="viewContext">The view context.</param>
		/// <param name="writer">The writer that is used to render the view to the response.</param>
		/// <param name="instance">The <see cref="T:System.Web.Mvc.WebViewPage" /> instance.</param>
		protected override void RenderView(ViewContext viewContext, TextWriter writer, object instance)
		{
			if (writer == null)
			{
				throw new ArgumentNullException(nameof(writer));
			}

			var placeholderKeys = this.GetPlaceholders(this.ViewPath);
			var componentName = Path.GetFileNameWithoutExtension(this.ViewPath)?.Replace("-", string.Empty);
			var props = this.GetProps(viewContext.ViewData.Model, placeholderKeys);
		    string pageKey = GetPageKey(HttpContext.Current.Request);

		    var componentId = GetComponentRenderingContextKey(pageKey, componentName, String.Join("|", placeholderKeys));

            IReactComponent reactComponent = this.Environment.CreateComponent($"Components.{componentName}", props, componentId);
            bool isEditingOverrideEnabled = ReactSettingsProvider.Current.DisableClientSideWhenEditing && Sitecore.Context.PageMode.IsExperienceEditorEditing;

		    bool enableClientSide = ReactSettingsProvider.Current.EnableClientside && !isEditingOverrideEnabled;

            // Client side bundling 
            // Added by Thomas Tyack. https://aceik.com.au/2018/06/28/sitecore-page-speed-part-3-eliminate-js-loading-time/  
            // Page Speed updates allowing all the javascript loading to be defered. 
		    bool hasDynamicLoadingEnabled = Context.Item.Fields.Any(x => x.Name.Equals("EnableDynamicScriptLoadingOnPage") && x.Value == "1");
		    bool enableClientSideBundle = ReactSettingsProvider.Current.EnableGroupedClientsideScripts && hasDynamicLoadingEnabled && !isEditingOverrideEnabled;
		    bool enableClientSideDefer = ReactSettingsProvider.Current.EnableDeferClientsideScripts && hasDynamicLoadingEnabled && !isEditingOverrideEnabled;

            if (enableClientSideBundle)
		    {
		        writer.WriteLine(reactComponent.RenderHtml(false, true)); // Render the markup but not the client side JS yet.
		        this.ConstructFastBundle(reactComponent); // Add the client side JS to a bundle for rendering at the end of the page.
            }
		    else if(enableClientSideDefer)
		    {
		        writer.WriteLine(reactComponent.RenderHtml());
		        var tagBuilder = new TagBuilder("script")
		        {
		            InnerHtml = RenderJavascriptSingleBoostrap(reactComponent)
                };
		        tagBuilder.Attributes.Add("defer", "defer");
                writer.Write(System.Environment.NewLine);
		        writer.Write(tagBuilder.ToString());

            } else if (enableClientSide)
		    {
		        writer.WriteLine(reactComponent.RenderHtml());

		        var tagBuilder = new TagBuilder("script")
		        {
		            InnerHtml = reactComponent.RenderJavaScript()
                };

		        writer.Write(System.Environment.NewLine);
		        writer.Write(tagBuilder.ToString());
		    }
		    else
		    {
		        writer.WriteLine(reactComponent.RenderHtml(renderServerOnly: true));
		    }
        }

		private IReactEnvironment Environment
		{
			get
			{
				try
				{
					return ReactEnvironment.Current;
				}
				catch (TinyIoCResolutionException ex)
				{
					throw new ReactNotInitialisedException("ReactJS.NET has not been initialised correctly.", ex);
				}
			}
		}

        private string RenderJavascript(IReactComponent reactComponent)
        {
            return "if(document.getElementById(\"" + reactComponent.ContainerId + "\")){" + ApplyFilters(reactComponent.RenderJavaScript()) + " }" + System.Environment.NewLine;
        }

	    private string RenderJavascriptSingleBoostrap(IReactComponent reactComponent)
	    {
            string exception1 = string.Empty;
            if (ReactSettingsProvider.Current.Exception1Enabled && new ID(ReactSettingsProvider.Current.Exception1RenderingId) == RenderingContext.Current.Rendering?.RenderingItem?.ID)
            {
                exception1 = ReactSettingsProvider.Current.Exception1MinifiedInlineJs;
            }

	        string mainBody = "if(document.getElementById(\"" + reactComponent.ContainerId + "\")){" + ApplyFilters(reactComponent.RenderJavaScript()) + " }" + System.Environment.NewLine;
	        return exception1 + "reactJsxNamespace." + ApplyFunctionFilters(reactComponent.ContainerId) + " = function(){"+mainBody+"};";
        }

        /// <summary>
        /// Adds a component to the bundle for a particular page. 
        /// </summary>
        /// <param name="reactComponent">The React component to add.</param>
	    private void ConstructFastBundle(IReactComponent reactComponent)
	    {
            // Lookup the rendering script for this rendering
            var renderingJavascript = _bundleCache.GetOrAddToCache(
                reactComponent.ContainerId,
                () =>
                {
                    return _bundleCache.GetOrAddToCache(reactComponent.ContainerId, () => RenderJavascript(reactComponent));
                });

            var bundle = GetBundle(reactComponent, renderingJavascript);
            string bundleObjKey = GetBundleKey();

            if (!bundle.ComponentBundle.ContainsKey(reactComponent.ContainerId))
            {
                bundle.ComponentBundle.Add(reactComponent.ContainerId, renderingJavascript);
                _bundleCache.Set(bundleObjKey, bundle);                
                _bundleCache.Set(GetBundleKey(isBundleObject: false, isBundleHtml: true), RenderPageScriptsFresh(bundle));
            }
        }

        private PageBundle GetBundle(IReactComponent reactComponent, string renderingJs)
        {
            string bundleObjKey = GetBundleKey();
            var currentBundle = _bundleCache.Get<PageBundle>(bundleObjKey);
            if (currentBundle == null)
            {
                // Lookup the bundle see if it already exists in cache. If not create it.
                currentBundle = _bundleCache.GetOrAddToCache<PageBundle>(
                    bundleObjKey,
                    () =>
                    {
                        var newBundle = new PageBundle() { PageKey = bundleObjKey, ComponentBundle = new Dictionary<string, string>() };
                        newBundle.ComponentBundle.Add(reactComponent.ContainerId, renderingJs);
                        return newBundle;
                    });
            }

            return currentBundle;
        }

        /// <summary>
        /// Generates a component key, for the bundling cache.
        /// Takes into consideration content ITem, component name, databasrouce and Personalisation rules.
        /// </summary>
        /// <param name="pageKey"></param>
        /// <param name="name"></param>
        /// <param name="placeholderKeys"></param>
        /// <returns></returns>
        private string GetComponentRenderingContextKey(string pageKey, string name, string placeholderKeys)
	    {
            var datasourceId = RenderingContext.Current.Rendering?.Item?.ID.ToShortID().ToString();
	        string componentId = $"{pageKey}:{name}:{datasourceId}:{GeneratePersonlisationKey()}";

	        return componentId;
	    }

        protected virtual string[] GetPlaceholders(string viewPath)
		{
			const string noPlaceholders = "NONE";

		    const string placeholderRegEx = @"<Placeholder\b[^>]*>";
		    const string keyRegEx = "placeholderKey={['\"](?<name>[\\$A-Za-z0-9\\.\\-_\\ ]+)['\"]}";
            const string isDynamicRegEx = "isDynamic=[{]?['\"]?(?<dynamic>\\w +)['\"]?[}]?";

            var htmlCache = Caching.CacheManager.GetHtmlCache(Context.Site);
            var cacheKey = $"$React.PlaceholderKeys.{viewPath}";
            var keys = htmlCache?.GetHtml(cacheKey);
            if (!string.IsNullOrWhiteSpace(keys))
            {
                return keys.Equals(noPlaceholders) ? new string[0] : keys.Split(Constants.Comma, StringSplitOptions.RemoveEmptyEntries);
            }

            var placeholderKeys = new List<string>();

            // TODO: remove the dependency on HttpContext.Current here
            var jsxContents = File.ReadAllText(HttpContext.Current.Server.MapPath(viewPath));
		    if (string.IsNullOrWhiteSpace(jsxContents))
		    {
		    	return new string[0];
		    }

		    var regex = new Regex(placeholderRegEx);
		    var matches = regex.Matches(jsxContents);

		    foreach (Match match in matches)
		    {
                var plRegEx = new Regex(keyRegEx);
		        var placeholderKeyMatches = plRegEx.Matches(match.Value);

                if (placeholderKeyMatches.Count == 1)
		        {
                    // there should only ever be ONE
		            var dynRexEx = new Regex(isDynamicRegEx);
		            var isDynamicMatches = dynRexEx.Matches(match.Value);

		            var dynamicPrefix = string.Empty;
		            if (isDynamicMatches.Count == 1)
		            {
		                bool isDynamic;
		                // there should only ever be ONE
		                bool.TryParse(isDynamicMatches[0].Value, out isDynamic);
		                dynamicPrefix = isDynamic ? "$Id." : string.Empty;
		            }

		            placeholderKeys.Add(dynamicPrefix + placeholderKeyMatches[0].Groups["name"].Value);
                }
            }

            // Make sure we set the cache with the placeholder keys. Even if there are not 
            // any keys - this should make subsequent calls faster as they will not have to 
            // do the regex search.
            htmlCache?.SetHtml(cacheKey, placeholderKeys.Any() ? string.Join(",", placeholderKeys) : noPlaceholders);

            return placeholderKeys.ToArray();
        }

	    internal Rendering Rendering
	    {
	        get { return RenderingContext.Current.Rendering; }
	    }

	    protected virtual dynamic GetProps(object viewModel, string[] placeholderKeys)
		{
			dynamic props = new ExpandoObject();
			var propsDictionary = (IDictionary<string, object>)props;

			dynamic placeholders = new ExpandoObject();
			var placeholdersDictionary = (IDictionary<string, object>)placeholders;

			propsDictionary["placeholder"] = placeholders;
            propsDictionary["data"] = viewModel;
            propsDictionary["isSitecore"] = true;
            propsDictionary["isEditing"] = Context.PageMode.IsExperienceEditor;

			if (!placeholderKeys.Any())
			{
				return props;
			}

			var controlId = this.Rendering.Parameters["id"] ?? string.Empty;
			dynamic placeholderId = null;

			foreach (var placeholderKey in placeholderKeys)
			{
				if (placeholderKey.StartsWith("$Id."))
				{
					if (placeholderId == null)
					{
						placeholderId = new ExpandoObject();
						placeholdersDictionary["$Id"] = placeholderId;
					}

					((IDictionary<string, object>)placeholderId)[placeholderKey.Mid(3)] = PageContext.Current.HtmlHelper.Sitecore().Placeholder(controlId + placeholderKey.Mid(3)).ToString();
				}
				else
				{
					placeholdersDictionary[placeholderKey] = PageContext.Current.HtmlHelper.Sitecore().Placeholder(placeholderKey).ToString();
				}
			}

			return props;
		}

        #region [Bundling]

        public static HtmlString RenderPageScripts()
        {
            CustomCacheService customCacheService = new CustomCacheService();
            string bundleKey = GetBundleKey(isBundleObject: false, isBundleHtml: true);
            return new HtmlString(customCacheService.Get<string>(bundleKey));
        }

        public static string RenderPageScriptsFresh(PageBundle pageBundle)
        {
            HtmlString htmlString = new HtmlString("");
            if (pageBundle != null)
            {
                StringBuilder stringBuilder = new StringBuilder();
                var info = System.Environment.NewLine + $"var bundleCount =  {pageBundle.ComponentBundle.Values.Count};" + System.Environment.NewLine;
                stringBuilder.Append(info);
                stringBuilder.Append("function reactClientsBootstrap(){");
                foreach (string value in pageBundle.ComponentBundle.Values)
                {
                    stringBuilder.Append(value);
                }
                stringBuilder.Append("}");
                TagBuilder val = new TagBuilder("script");
                val.Attributes.Add("defer", "defer");
                val.InnerHtml = (stringBuilder.ToString());
                htmlString = new HtmlString(((object)val).ToString());
            }
            return htmlString.ToString();
        }
        #endregion 

        #region [Keys]
        public static string GetPageKey(HttpRequest request)
        {
            string key = request.Url.PathAndQuery;
            if (key == "/")
            {
                string contextItemId = string.Empty;
                if (RenderingContext.Current.ContextItem != null)
                {
                    contextItemId = RenderingContext.Current.PageContext.Item?.ID.ToShortID().ToString();
                }
                key = $"homepage-" + contextItemId;
            }
            else
            {
                key = Sitecore.Context.Site.Name + request.Url.PathAndQuery.Replace("/", "_");
            }
            return key.ToLower();
        }

        public string GeneratePersonlisationKey()
	    {
	        string cacheKey = string.Empty;

	        try
	        {
	            var allReferences = GetRenderingsForControl().ToList();
	            var renderingUniqueId = ID.Parse(RenderingContext.Current.Rendering.UniqueId);
	            var renderingReferrence = allReferences.FirstOrDefault(i => ID.Parse(i.UniqueId).Equals(renderingUniqueId));
	            if (renderingReferrence?.Settings?.Rules != null)
	            {
	                var ruleContext = new ConditionalRenderingsRuleContext(allReferences, renderingReferrence);
                    if(ruleContext != null)
                    {
                        renderingReferrence.Settings.Rules.RunFirstMatching(ruleContext);
                        string personalizedDatasource = ruleContext.Reference.Settings.DataSource;
                        if(!string.IsNullOrWhiteSpace(personalizedDatasource))
                        {
                            cacheKey += String.Concat("pd:", new ID(new Guid(personalizedDatasource)).ToShortID().ToString());
                            string profileMatch = GetPatterName(ruleContext, personalizedDatasource);
                            if (!string.IsNullOrWhiteSpace(profileMatch))
                            {
                                cacheKey += String.Concat(":pn:", profileMatch);
                            }
                        }
                    }
	            }
            }
            catch (Exception ex)
	        {
                Sitecore.Diagnostics.Log.Debug("Sitecore.React could not identify a personalisation for the current rendering", ex);
	        }

	        return cacheKey;
	    }

        public string GeneratePagePersonlisationKey()
        {
            //IL_0042: Unknown result type (might be due to invalid IL or missing references)
            //IL_0049: Expected O, but got Unknown
            //IL_007b: Unknown result type (might be due to invalid IL or missing references)
            string text = string.Empty;
            try
            {
                List<RenderingReference> list = GetRenderingsForControl().ToList();
                if (list.Any())
                {
                    foreach (RenderingReference item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item.RenderingItem.Conditions))
                        {
                            ConditionalRenderingsRuleContext val = new ConditionalRenderingsRuleContext(list, item);
                            item.Settings.Rules.RunFirstMatching(val);
                            string dataSource = val.Reference.Settings.DataSource;
                            text = text + "pd:" + ((object)new ID(new Guid(dataSource)).ToShortID()).ToString();
                            string profileMatch = GetPatterName(val, dataSource);
                            if(!string.IsNullOrWhiteSpace(profileMatch))
                            {
                                text = text + "pn:" + ((object)new ID(new Guid(dataSource)).ToShortID()).ToString();
                            }
                        }
                    }
                    return text;
                }
                return text;
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("Sitecore.React could not identify a combined personalization key", (object)ex);
                return text;
            }
        }

        /// <summary>
        /// If the rendering has rules attached lookup profile card associated with the Rule run.
        /// Return empty if it can't be found.
        /// </summary>
        /// <param name="ruleContext"></param>
        /// <param name="personalizedDatasource"></param>
        /// <returns></returns>
        public string GetPatterName(ConditionalRenderingsRuleContext ruleContext, string personalizedDatasource)
        {
            try
            {
                foreach (var rule in ruleContext.Reference.Settings.Rules.Rules)
                {
                    foreach (var action in rule.Actions)
                    {
                        var setDataSourceAction = action as SetDataSourceAction<ConditionalRenderingsRuleContext>;
                        if (setDataSourceAction != null && setDataSourceAction.DataSource == personalizedDatasource)
                        {
                            var conditionMatch = rule.Condition as Sitecore.Analytics.Rules.Conditions.HasPatternCondition<ConditionalRenderingsRuleContext>;
                            if (conditionMatch != null)
                            {
                                return conditionMatch.PatternName.ToLower().GetHashString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("Could not locate profile card match for rendering", ex);
            }
            return string.Empty;
        }

        public static string GetBundleKey(bool isBundleObject = true, bool isBundleHtml = false)
        {
            string pageKey = GetPageKey(HttpContext.Current.Request);
            if (isBundleObject)
            {
                return pageKey + "bundleObject";
            }
            if (isBundleHtml)
            {
                return pageKey + "bundleHtml";
            }
            return pageKey;
        }

	    private string ApplyFilters(string scripts)
	    {
	        if (scripts.IndexOf("__RequestVerificationToken") > -1)
	        {
	            string pattern = @"name=\\u0022__RequestVerificationToken\\u0022 type=\\u0022hidden\\u0022 value=\\u0022([A-Za-z0-9+=/\-\\_]+?)\\u0022 /\\u003e";
	            Regex rgx = new Regex(pattern);
	            string replacement = @"name=\u0022__RequestVerificationToken\u0022 type=\u0022hidden\u0022 value=\u0022\u0022 /\u003e";
	            scripts = rgx.Replace(scripts, replacement);
	        }
	        return scripts;
	    }

	    private string ApplyFunctionFilters(string name)
	    {
	        return name.Replace(":", string.Empty);
	    }

        #endregion

        public RenderingReference[] GetRenderingsForControl()
	    {
	        var item = Sitecore.Context.Item;
	        if (item != null)
	        {
	            var device = Sitecore.Context.Device;
	            var renderings = item.Visualization.GetRenderings(device, true);

	            return renderings;
	        }
	        return new RenderingReference[0];
	    }
    }
}
