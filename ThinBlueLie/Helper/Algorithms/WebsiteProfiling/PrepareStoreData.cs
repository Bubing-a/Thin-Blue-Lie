﻿using DataAccessLibrary.Enums;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using ThinBlueLie.Models;
using static DataAccessLibrary.Enums.MediaEnums;

namespace ThinBlueLie.Helper.Algorithms.WebsiteProfiling
{
    public static partial class WebsiteProfile
    {
        /// <summary>
        /// Gets url of source for reddit links
        /// </summary>
        static async Task<ViewMedia> GetRedditDataAsync(ViewMedia media)
        {
            var url = media.SourcePath;
            var source = media.SourceFrom;
            url = url.Remove(url.Length - 1, 1).Insert(url.Length - 1, ".json");
            using (var httpClient = new HttpClient())
            {
                var json = await httpClient.GetStringAsync(url);
                dynamic stuff = JsonConvert.SerializeObject(json);
                string path = stuff.url_overridden_by_dest;
                Uri uri = new Uri(path);
                if (uri.AbsolutePath.Substring(uri.AbsolutePath.IndexOf(".")) == ".jpg")
                    path = uri.AbsolutePath.Remove(uri.AbsolutePath.Length - 4, 4).Remove(0, 1);
                if (uri.Host.Contains("youtu.be") || uri.Host.Contains("youtube.com"))
                    source = SourceFromEnum.Youtube;
                media.SourcePath = url;
                media.SourceFrom = source;
                return media;
            }
        }
        public static async Task<ViewMedia> PrepareStoreData(ViewMedia media)
        {
            Uri myUri = new Uri(media.SourcePath);
            string host = myUri.Host;
            if (media.SourceFrom == SourceFromEnum.Youtube)
            {
                if (host.Contains("youtube.com"))
                {
                    Uri uri = new Uri(media.SourcePath, UriKind.Absolute);
                    media.SourcePath = HttpUtility.ParseQueryString(uri.Query).Get("v"); //only stores youtube's video id
                    return media;
                }
            }
            if (media.SourceFrom == SourceFromEnum.Reddit)
            {
                if (media.MediaType == MediaTypeEnum.Image)  //directly in image
                {
                    if (host.Contains("preview.redd.it"))
                    {
                        media.SourceFrom = SourceFromEnum.Link;
                        return media;
                    }
                    if (host.Contains("preview.redd.it"))
                    {
                        Uri uri = new Uri(media.SourcePath);
                        media.SourcePath = uri.AbsolutePath.Remove(uri.AbsolutePath.Length - 4, 4).Remove(0, 1);
                        return media;
                    }                   
                }
                else if (host.Contains("reddit.com")) //for everything in the comments page
                {
                    media = await GetRedditDataAsync(media);
                    return media;
                }
            }

            return media;
        }
    }
}
