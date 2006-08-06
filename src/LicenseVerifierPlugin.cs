/***************************************************************************
 *  LicenseVerifierPlugin.cs
 *
 *  Copyright (C) 2006 Luke Hoersten
 *  Written by Luke Hoersten <luke.hoersten@gmail.com>
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
using System.Data;
using System.Collections;
using Mono.Unix;

using CreativeCommons;
using Banshee.Base;
using Banshee.Kernel;

namespace Banshee.Plugins.LicenseVerifier 
{
    public class LicenseVerifierPlugin : Banshee.Plugins.Plugin
    {
        protected override string ConfigurationName { get { return "LicenseVerifier"; } }
        public override string DisplayName { get { return "License Verifier"; } }
        
        public override string Description {
            get {
                return Catalog.GetString(
                    "Automatically verify Creative Commons licenses embedded " + 
                    "in song metadata."
                    );
            }
        }

        public override string [] Authors {
            get { return new string [] { "Luke Hoersten" }; }
        }

        public event EventHandler ScanStarted;
        public event EventHandler ScanEnded;
        
        private int generation;
        private int scan_ref_count;
       
        protected override void PluginInitialize()
        {
            System.Threading.Interlocked.Increment(ref generation);
            System.Threading.Interlocked.Exchange(ref scan_ref_count, 0);
            
            if(Globals.Library.IsLoaded) {
                ScanLibrary();
            } else {
                Globals.Library.Reloaded += OnLibraryReloaded;
            }
            
            Globals.Library.TrackAdded += OnLibraryTrackAdded;
        }
        
        protected override void PluginDispose()
        {
            System.Threading.Interlocked.Exchange(ref scan_ref_count, 0);
            
            Globals.Library.Reloaded -= OnLibraryReloaded;
            Globals.Library.TrackAdded -= OnLibraryTrackAdded;
        }
        
        public override Gtk.Widget GetConfigurationWidget()
        {            
            return new LicenseVerifierConfigPage(this);
        }
        
        // ----------------------------------------------------

        protected virtual void OnScanStarted()
        {
            System.Threading.Interlocked.Increment(ref scan_ref_count);
        
            EventHandler handler = ScanStarted;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        protected virtual void OnScanEnded()
        {
            System.Threading.Interlocked.Decrement(ref scan_ref_count);
        
            EventHandler handler = ScanEnded;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }

        private void OnLibraryReloaded(object o, EventArgs args)
        {
            ScanLibrary();
        }

        private void OnLibraryTrackAdded(object o, LibraryTrackAddedArgs args)
        {
            Scheduler.Schedule(new ProcessTrackJob(this, args.Track));
        }

        internal void RescanLibrary()
        {
            Globals.Library.Db.Query("UPDATE TrackLicenses SET LicenseVerifyStatus = 0");
            ScanLibrary();
        }
                
        private void ScanLibrary()
        {
            Scheduler.Schedule(new ScanLibraryJob(this));
        }

        internal bool IsScanning {
            get { return scan_ref_count > 0; }
        }

        private class ScanLibraryJob : IJob
        {
            private LicenseVerifierPlugin plugin;
            private int generation;
            
            public ScanLibraryJob(LicenseVerifierPlugin plugin)
            {
                this.plugin = plugin;
                this.generation = plugin.generation;
            }
        
            public void Run()
            {
                if(generation != plugin.generation) {
                    return;
                }
                
                plugin.OnScanStarted();
                
                IDataReader reader = Globals.Library.Db.Query(
                    @"SELECT TrackID 
                      FROM TrackLicenses 
                      WHERE LicenseVerifyStatus IS NULL
                      OR LicenseVerifyStatus = 0");
                
                while(reader.Read()) {
                    Scheduler.Schedule(new ProcessTrackJob(plugin, Convert.ToInt32(reader["TrackID"])));
                }
                
                reader.Dispose();
                
                plugin.OnScanEnded();
            }
        }
        
        private class ProcessTrackJob : IJob
        {
            private LibraryTrackInfo track;
            private int track_id;
            private LicenseVerifierPlugin plugin;
            private int generation;
            
            public ProcessTrackJob(LicenseVerifierPlugin plugin, LibraryTrackInfo track)
            {
                this.plugin = plugin;
                this.track = track;
                this.generation = plugin.generation;
            }
            
            public ProcessTrackJob(LicenseVerifierPlugin plugin, int trackId)
            {
                this.plugin = plugin;
                this.track_id = trackId;
                this.generation = plugin.generation;
            }
        
            public void Run()
            {
                if(plugin.generation != generation) {
                    return;
                }
                
                ProcessTrack(track != null ? track : Globals.Library.GetTrack(track_id));
            }

            private void ProcessTrack(LibraryTrackInfo track)
            {   
                if(track == null) {
                    return;
                }
                
                VerifyLicense(track);
                track.Save();
            }
            
            private static void VerifyLicense(TrackInfo track)
            {
                /* Step 1: Not complete license claim */
                if(track.LicenseUri == null || track.MetadataUri == null) {
                    track.LicenseVerifyStatus = LicenseVerifyStatus.NoAttempt;
                    return;
                }

                /* Step 2: Verify license */
                if(!Verifier.VerifyLicense(track.LicenseUri, track.Uri.AbsolutePath, new Uri (track.MetadataUri)))
                    track.LicenseVerifyStatus = LicenseVerifyStatus.Invalid;
                else
                    track.LicenseVerifyStatus = LicenseVerifyStatus.Valid;
            }
        }
    }
}
