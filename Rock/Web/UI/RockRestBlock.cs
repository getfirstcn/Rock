// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using Rock.Blocks;
using Rock.Model;
using Rock.Net;
using Rock.Web.Cache;

namespace Rock.Web.UI
{
    /// <summary>
    /// Block that interfaces with the browser via non-web-forms
    /// </summary>
    /// <seealso cref="Rock.Web.UI.RockBlock" />
    /// <seealso cref="Rock.Blocks.IRockRestBlockType" />
    public abstract class RockRestBlock : RockBlock, IRockBlockType
    {
        /// <summary>
        /// Gets or sets the request context.
        /// </summary>
        /// <value>
        /// The request context.
        /// </value>
        public RockRequestContext RequestContext { get; set; }

        /// <summary>
        /// Returns the currently logged in person. If user is not logged in, returns null
        /// </summary>
        public new Person CurrentPerson
        {
            get { return RequestContext.CurrentPerson; }
        }

        /// <summary>
        /// Gets the block cache.
        /// </summary>
        /// <value>
        /// The block cache.
        /// </value>
        public new BlockCache BlockCache
        {
            get => base.BlockCache;
            set => base.BlockCache = value;
        }

        /// <summary>
        /// Gets or sets the page cache.
        /// </summary>
        /// <value>
        /// The page cache.
        /// </value>
        public new PageCache PageCache
        {
            get => base.PageCache;
            set => base.PageCache = value;
        }

        /// <summary>
        /// Gets the root element identifier. Should be something like upnlMyBlock.ClientId
        /// </summary>
        /// <returns></returns>
        protected abstract string GetRootElementId();

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            // Only register scripts on the initial load of the page
            if ( IsPostBack)
            {
                return;
            }

            // Include the RockRestBlocks bundle
            if ( !Page.ClientScript.IsStartupScriptRegistered( "RockRestBlocks" ) )
            {
                Page.ClientScript.RegisterStartupScript( GetType(), "RockRestBlocks",
                    $@"<script type=""text/javascript"" src=""/Scripts/Bundles/RockRestBlocks""></script>" );
            }

            // Register the javascript "code-behind" for this block type if not already done
            // Path comes as "~/Blocks/Security.Login.ascx". Remove the ~ or the resolved URL would be /page/Blocks/Security...
            var blockTypePath = BlockCache.BlockType.Path.Replace( "~", string.Empty );

            if ( !Page.ClientScript.IsStartupScriptRegistered( blockTypePath ) )
            {
                Page.ClientScript.RegisterStartupScript( GetType(), blockTypePath,
                    $@"<script type=""text/javascript"" src=""{blockTypePath}.js""></script>" );
            }

            // Register the script to initialize this instance of the block using JS code-behind
            var blockIdentifier = $"RockRestBlock-{BlockCache.Guid}";

            if ( !Page.ClientScript.IsStartupScriptRegistered( blockIdentifier ) )
            {
                // Block namespace will be like: "Blocks/Security/Login"
                var blockNamespace = blockTypePath.Substring( 1 ).ReplaceLastOccurrence( ".ascx", string.Empty );

                Page.ClientScript.RegisterStartupScript( GetType(), blockIdentifier,
                    $@"<script type=""text/javascript"">
                        window.Rock.RestBlocks['{blockNamespace}']({{
                            rootElement: document.getElementById('{GetRootElementId()}'),
                            pageId: {BlockCache.PageId},
                            blockId: {BlockCache.Id},
                            blockAction: window.Rock.RestBlocks.blockActionFactory({{
                                pageId: {BlockCache.PageId},
                                blockId: {BlockCache.Id}
                            }})
                        }});
                    </script>" );
            }
        }
    }
}
