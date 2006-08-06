
/***************************************************************************
 *  MMKeysConfigPage.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@aaronbock.net>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.IO;
using Gtk;
using GConf;
using Mono.Unix;

using Banshee.Base;
using Banshee.Widgets;

namespace Banshee.Plugins.LicenseVerifier 
{
    public class LicenseVerifierConfigPage : VBox
    {
        private LicenseVerifierPlugin plugin;
        private Button rescan_button;
        
        public LicenseVerifierConfigPage(LicenseVerifierPlugin plugin) : base()
        {
            this.plugin = plugin;
            BuildWidget();
        }
        
        private void BuildWidget()
        {    
            Spacing = 10;
            
            Label title = new Label();
            title.Markup = String.Format("<big><b>{0}</b></big>", 
                GLib.Markup.EscapeText(Catalog.GetString("License Verifier")));
            title.Xalign = 0.0f;

            Label label = new Label(Catalog.GetString(
                "Rescanning the library will reverify all licenses."));
            label.Wrap = true;
            label.Xalign = 0.0f;

            rescan_button = new Button (Catalog.GetString ("Rescan Library"));
            rescan_button.Sensitive = !plugin.IsScanning;
            rescan_button.Clicked += OnRescan;
            HBox rescan_box = new HBox ();
            rescan_box.PackStart(rescan_button, false, false, 0);
            rescan_box.PackStart(new Label(""), true, true, 0);
            
            PackStart(title, false, false, 0);
            PackStart(label, false, false, 0);
            PackStart(rescan_box, false, false, 0);
            
            ShowAll();
            
            plugin.ScanStarted += OnScanStarted;
            plugin.ScanEnded += OnScanEnded;
        }
        
        private void OnScanStarted(object o, EventArgs args)
        {
            Application.Invoke(delegate {
                rescan_button.Sensitive = false;
            });
        }

        private void OnScanEnded(object o, EventArgs args)
        {
            Application.Invoke(delegate {
                rescan_button.Sensitive = true;
            });
        }

        private void OnRescan(object o, EventArgs args)
        {
            plugin.RescanLibrary();
            rescan_button.Sensitive = false;
        }
    }
}
