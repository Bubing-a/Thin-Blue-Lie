﻿using DataAccessLibrary.Enums;
using System;
using ThinBlueLie.Models;

namespace ThinBlueLie.Helper.Algorithms.WebsiteProfiling
{
    public static partial class WebsiteProfile
    {
        //go from 0989uwefbwef to youtube.com/watch?v=0989uwefbwef
        public static string PrepareSendData(ViewMedia media)
        {
            if (media.SourceFrom == MediaEnums.SourceFromEnum.Youtube)
            {
                var builder = new UriBuilder
                {
                    Host = "youtube.com",
                    Scheme = "https",
                    Query = "v=" + media.SourcePath
                };

                return builder.Uri.ToString();
            }
            if (media.SourceFrom == MediaEnums.SourceFromEnum.Reddit)
            {
                return media.SourcePath;
            }
            return media.SourcePath;
        }

    }
}
