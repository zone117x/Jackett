﻿using System;
using System.IO;
using System.Web;

namespace Jackett.Utils
{
    public class DataUrlUtils
    {
        public static string ReadFileToDataUrl(string file)
        {
            string mime = MimeMapping.GetMimeMapping(file);
            return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(file));
        }

        public static string BytesToDataUrl(byte[] bytes, string mimeType = "image/jpg")
        {
            return "data:" + mimeType + ";base64," + Convert.ToBase64String(bytes);
        }
    }
}
