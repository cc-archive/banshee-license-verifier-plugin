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
        
//        public override Gtk.Widget GetConfigurationWidget()
//        {            
//            return new LicenseVerifierConfigPage(this);
//        }
        
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
            Globals.Library.Db.Query("UPDATE Tracks SET LicenseVerifyStatus = 0");
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
                //Console.WriteLine("Scanning library for tracks to update");
                
                IDataReader reader = Globals.Library.Db.Query(
                    @"SELECT TrackID 
                        FROM Tracks 
                        WHERE LicenseVerifyStatus IS NULL
                            OR LicenseVerifyStatus = 0"
                );
                
                while(reader.Read()) {
                    Scheduler.Schedule(new ProcessTrackJob(plugin, Convert.ToInt32(reader["TrackID"])));
                }
                
                reader.Dispose();
                
                //Console.WriteLine("Done scanning library");
                plugin.OnScanEnded();
            }
        }
        
        private class ProcessTrackJob : IJob
        {
            private const string LICENSES_STRING = "licenses/";
            private const int LICENSES_LENGTH = 9;
            private const string HTTP_STRING = "http://";
            private const string VERIFY_STRING = "verify at ";
            private const int VERIFY_LENGTH = 10;
        
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
                
                track.LicenseVerifyStatus = VerifyLicense(track);
                
                track.Save();
            }
            
            public static LicenseVerifyStatus VerifyLicense(TrackInfo track)
            {
                try {
                    /* Step 1: Not complete license claim */
                    if(!FullLicenseClaim(track))
                        return LicenseVerifyStatus.Failure;
                
                    /* Step 2: Verify license */
                    string verified_license_uri = null;
                    if(Verifier.VerifyLicense (track.LicenseUri, track.Uri.AbsolutePath,
                                               new Uri (track.MetadataUri)))
                        verified_license_uri = track.LicenseUri;
                    else
                        return LicenseVerifyStatus.Failure;

                    /* Step 3: Store license attribute string in track metadata */
                    track.License = GetLicenseAttributes(verified_license_uri);
                    return LicenseVerifyStatus.Success;
                } catch(LicenseParseException e) {
                    Console.WriteLine(e);
                    return LicenseVerifyStatus.Failure;
                }
            }
            
            private static bool FullLicenseClaim(TrackInfo track)
            {
                if(track.Copyright == null) {
                    if(track.LicenseUri == null || track.MetadataUri == null) {
                        return false;
                    }
                } else {
                    track.LicenseUri = ParseLicenseUri(track.Copyright);
                    track.MetadataUri = ParseMetadataUri(track.Copyright);
                }
                return true;
            }
            
            private static string GetLicenseAttributes(string data)
            {
                int licenses_index = data.ToLower().IndexOf(LICENSES_STRING);
                if(licenses_index <= 0) {
                    throw new LicenseParseException("No attributes were found in Copyright tag.");
                }
                
                int attribute_index = licenses_index + LICENSES_LENGTH;
                return data.Substring(attribute_index, data.IndexOf('/', attribute_index) - attribute_index);
            }
            
            private static string ParseLicenseUri(string data)
            {
                int verify_index = (data.ToLower()).IndexOf(VERIFY_STRING);
                if(verify_index <= 0) {
                    throw new LicenseParseException("No metadata was found in Copyright tag while parsing license URL.");    
                }
                
                int http_index = (data.ToLower()).LastIndexOf(HTTP_STRING, verify_index);
                if(http_index <= 0) {
                    throw new LicenseParseException("No license was found in Copyright tag.");
                }
                
                /* The -1 is because LastIndexOf adds 1 for arg test */
                return data.Substring(http_index, (verify_index - http_index) - 1);
            }

            private static string ParseMetadataUri(string data)
            {
                int verify_index = data.ToLower().IndexOf(VERIFY_STRING);
                if(verify_index <= 0) {
                    throw new LicenseParseException("No metadata was found in Copyright tag at parsing metadata URL.");
                }
                
                int metadata_index = verify_index + VERIFY_LENGTH;
                return data.Substring(metadata_index, data.Length - (metadata_index));
            }
        }
        
        public class LicenseParseException : ApplicationException
        {
            public LicenseParseException()
            {
            }
            
            public LicenseParseException(string m) : base(m)
            {
            }
        }
    }
}
