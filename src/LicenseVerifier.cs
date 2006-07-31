/***************************************************************************
 *  CreativeCommons.cs
 *
 *  Copyright (C) 2006 Luke Hoersten
 *  Written by Luke Hoersten <luke.hoersten@gmail.com>
 ****************************************************************************/

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Text;
using System.Security.Cryptography;
using CreativeCommons;

namespace Banshee.Base
{
    public class LicenseParseException : ApplicationException
    {
        public LicenseParseException()
        {
        }
        
        public LicenseParseException(string m) : base(m)
        {
        }
    }
    
    public static class CreativeCommons
    {
        private const string LICENSES_STRING = "licenses/";
        private const int LICENSES_LENGTH = 9;
        
        private const string HTTP_STRING = "http://";
        
        private const string VERIFY_STRING = "verify at ";
        private const int VERIFY_LENGTH = 10;
        
        public static void VerifyLicense(TrackInfo track)
        {
            try {
                /* Not complete license claim */
                if(!FullLicenseClaim(track))
                    return;
            
                /* Verify license */
                string verified_license_uri = null;
                if(Verifier.VerifyLicense (track.LicenseUri, track.Uri.AbsolutePath,
                                           new Uri (track.MetadataUri)))
                    verified_license_uri = track.LicenseUri;
                else
                    return;

                /* Store license attribute string in track metadata */
                track.License = GetLicenseAttributes(verified_license_uri);
            } catch(LicenseParseException e) {
                Console.WriteLine(e);
                return;
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
}
