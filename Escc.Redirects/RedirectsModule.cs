﻿using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Exceptionless;

namespace Escc.Redirects
{
    /// <summary>
    /// An HTTP module to redirect URLs which are not found locally
    /// </summary>
    public class RedirectsModule : IHttpModule
    {
        /// <summary>
        /// You will need to configure this module in the Web.config file of your
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        #region IHttpModule Members

        public void Dispose()
        {
            //clean-up code here.
        }

        public void Init(HttpApplication context)
        {
            if (context == null) throw new ArgumentNullException("context");
            context.BeginRequest += new EventHandler(RedirectsModule_BeginRequest);
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void RedirectsModule_BeginRequest(object sender, EventArgs e)
        {          
            try
            {
                // If the requested path exists, or it's too long for MapPath to handle, do nothing
                try
                {
                    if (File.Exists(HostingEnvironment.MapPath(HttpContext.Current.Request.Url.AbsolutePath)))
                    {
                        return;
                    }
                }
                catch (PathTooLongException)
                {

                    return;
                }

                // If the requested path matches an ignored pattern, do nothing
                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["Escc.Redirects.IgnorePaths"]))
                {
                    var ignorePaths = ConfigurationManager.AppSettings["Escc.Redirects.IgnorePaths"].Split(',');
                    foreach (var ignorePath in ignorePaths)
                    {
                        if (Regex.IsMatch(HttpContext.Current.Request.Url.PathAndQuery, ignorePath, RegexOptions.IgnoreCase))
                        {
                            return;
                        }
                    }
                }

                // Try to match the requested URL to a redirect and, if successful, run handlers for the redirect
                var matchers = new IRedirectMatcher[] {new SqlServerRedirectMatcher()};
                var handlers = new IRedirectHandler[] {new ConvertToAbsoluteUrlHandler(), new PreserveQueryStringHandler(), new DebugInfoHandler(), new GoToUrlHandler()};

                foreach (var matcher in matchers)
                {
                    var redirect = matcher.MatchRedirect(HttpContext.Current.Request.Url);
                    if (redirect != null)
                    {
                        foreach (var handler in handlers)
                        {
                            redirect = handler.HandleRedirect(redirect);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If there's a problem, publish the error and continue
                ex.ToExceptionless().Submit();
            }
        }
    }
}
