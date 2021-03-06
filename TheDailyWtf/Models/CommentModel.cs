﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using TheDailyWtf.Data;
using TheDailyWtf.Discourse;

namespace TheDailyWtf.Models
{
    public class CommentModel
    {
        private static readonly Regex ImgSrcRegex = new Regex(@"src=""(?<comment>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public string BodyHtml { get; set; }
        public string Username { get; set;}
        public DateTime PublishedDate { get; set; }
        public int? DiscoursePostId { get; set; }
        public string ImageUrl { get; set; }

        public static IEnumerable<CommentModel> GetFeaturedCommentsForArticle(ArticleModel article)
        {
            if (article.DiscourseTopicId != null)
            {
                var comments = DiscourseHelper.GetFeaturedCommentsForArticle(article.Id);
                return comments.Select(c => CommentModel.FromDiscourse(c));
            }

            return new CommentModel[0];
        }

        public static IEnumerable<CommentModel> FromArticle(ArticleModel article)
        {
            var comments = StoredProcs.Comments_GetComments(article.Id).Execute();
            return comments.Select(c => CommentModel.FromTable(c));
        }

        public static string TrySanitizeDiscourseBody(string body)
        {
            try
            {
                // image src attributes in Discourse comment bodies are relative,
                // make them absolute to avoid image 404s on comments overview

                string replaced = ImgSrcRegex.Replace(
                    body,
                    m =>
                    {
                        string value = m.Groups["comment"].Value;
                        if (value.StartsWith("//"))
                            return string.Format("src=\"{0}\"", value);

                        return string.Format("src=\"//{0}{1}\"", Config.Discourse.Host, value);
                    }
                );

                return replaced;
            }
            catch
            {
                return body;
            }
        }

        private static CommentModel FromTable(Tables.Comments comment)
        {
            return new CommentModel()
            {
                BodyHtml = comment.Discourse_Post_Id == null 
                    ? BbCodeFormatComment(comment.Body_Html) 
                    : comment.Body_Html,
                Username = comment.User_Name,
                DiscoursePostId = comment.Discourse_Post_Id,
                PublishedDate = comment.Posted_Date
            };
        }

        private static CommentModel FromDiscourse(Post post)
        {
            return new CommentModel()
            {
                BodyHtml = TrySanitizeDiscourseBody(post.BodyHtml),
                Username = post.Username,
                PublishedDate = post.PostDate,
                DiscoursePostId = post.Id,
                ImageUrl = post.ImageUrl
            };
        }

        private static string BbCodeFormatComment(string text)
        {
            string encodedString = HttpUtility.HtmlEncode(text);

            // Bold, Italic, Underline
            encodedString = Regexes.Bold.Replace(encodedString, "<b>$1</b>");
            encodedString = Regexes.Italic.Replace(encodedString, "<i>$1</i>");
            encodedString = Regexes.Underline.Replace(encodedString, "<u>$1</u>");

            // Quote
            if (Regexes.QuoteEndBbCode.Matches(encodedString).Count ==
                Regexes.QuoteStartBbCode.Matches(encodedString).Count + Regexes.EmptyQuoteStartBbCode.Matches(encodedString).Count)
            {
                encodedString = Regexes.QuoteStartBbCode.Replace(encodedString, "<BLOCKQUOTE class=\"Quote\"><div><i class=\"icon-quote\"></i> <strong>$1:</strong></div><div>");
                encodedString = Regexes.QuoteEndBbCode.Replace(encodedString, "</div></BLOCKQUOTE>");

                encodedString = Regexes.EmptyQuoteStartBbCode.Replace(encodedString, "<BLOCKQUOTE class=\"Quote\"><div>");
                encodedString = Regexes.EmptyQuoteEndBbCode.Replace(encodedString, "</div></BLOCKQUOTE>");
            };

            // Code
            encodedString = Regexes.Code.Replace(encodedString, "<pre>$1</pre>");

            // Anchors
            encodedString = Regexes.Url1.Replace(encodedString, "<a rel=\"nofollow\" href=\"http://www.$1\" target=\"_blank\" title=\"$1\">$1</a>");
            encodedString = Regexes.Url2.Replace(encodedString, "<a rel=\"nofollow\" href=\"$1\" target=\"_blank\" title=\"$1\">$1</a>");
            encodedString = Regexes.Url3.Replace(encodedString, "<a rel=\"nofollow\" href=\"$1\" target=\"_blank\" title=\"$1\">$3</a>");
            encodedString = Regexes.Url4.Replace(encodedString, "<a rel=\"nofollow\" href=\"$1\" target=\"_blank\" title=\"$1\">$3</a>");
            encodedString = Regexes.Link1.Replace(encodedString, "<a rel=\"nofollow\" href=\"$1\" target=\"_blank\" title=\"$1\">$1</a>");
            encodedString = Regexes.Link2.Replace(encodedString, "<a rel=\"nofollow\" href=\"$1\" target=\"_blank\" title=\"$1\">$3</a>");

            // Image
            encodedString = Regexes.Img1.Replace(encodedString, "<img src=\"$1\" border=\"0\" />");
            encodedString = Regexes.Img2.Replace(encodedString, "<img width=\"$1\" height=\"$3\" src=\"$5\" border=\"0\" />");

            // Color
            encodedString = Regexes.Color.Replace(encodedString, "<span style=\"color:$1;\">$3</span>");

            encodedString = encodedString.Replace("\n", "<br />");

            return encodedString;
        }

        private static class Regexes
        {
            private static readonly RegexOptions Options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            public static readonly Regex Bold = new Regex(@"\[b(?:\s*)\]((.|\n)*?)\[/b(?:\s*)\]", Regexes.Options);
            public static readonly Regex Italic = new Regex(@"\[i(?:\s*)\]((.|\n)*?)\[/i(?:\s*)\]", Regexes.Options);
            public static readonly Regex Underline = new Regex(@"\[u(?:\s*)\]((.|\n)*?)\[/u(?:\s*)\]", Regexes.Options);
            public static readonly Regex Code = new Regex(@"\[code(?:\s*)\]((.|\n)*?)\[/code(?:\s*)\]", Regexes.Options);
            public static readonly Regex Url1 = new Regex(@"\[url(?:\s*)\]www\.(.*?)\[/url(?:\s*)\]", Regexes.Options);
            public static readonly Regex Url2 = new Regex(@"\[url(?:\s*)\]((.|\n)*?)\[/url(?:\s*)\]", Regexes.Options);
            public static readonly Regex Url3 = new Regex(@"\[url=(?:""|&quot;|&#34;)((.|\n)*?)(?:\s*)(?:""|&quot;|&#34;)\]((.|\n)*?)\[/url(?:\s*)\]", Regexes.Options);
            public static readonly Regex Url4 = new Regex(@"\[url=((.|\n)*?)(?:\s*)\]((.|\n)*?)\[/url(?:\s*)\]", Regexes.Options);
            public static readonly Regex Link1 = new Regex(@"\[link(?:\s*)\]((.|\n)*?)\[/link(?:\s*)\]", Regexes.Options);
            public static readonly Regex Link2 = new Regex(@"\[link=((.|\n)*?)(?:\s*)\]((.|\n)*?)\[/link(?:\s*)\]", Regexes.Options);
            public static readonly Regex Img1 = new Regex(@"\[img(?:\s*)\]((.|\n)*?)\[/img(?:\s*)\]", Regexes.Options);
            public static readonly Regex Img2 = new Regex(@"\[img=((.|\n)*?)x((.|\n)*?)(?:\s*)\]((.|\n)*?)\[/img(?:\s*)\]", Regexes.Options);
            public static readonly Regex Color = new Regex(@"\[color=((.|\n)*?)(?:\s*)\]((.|\n)*?)\[/color(?:\s*)\]", Regexes.Options);

            public static readonly Regex QuoteStartBbCode = new Regex("\\[quote(?:\\s*)user=(?:\"|&quot;|&#34;)(.*?)(?:\"|&quot;|&#34;)\\]", Regexes.Options);
            public static readonly Regex QuoteEndBbCode = new Regex("\\[/quote(\\s*)\\]", Regexes.Options);

            public static readonly Regex EmptyQuoteStartBbCode = new Regex("\\[quote(\\s*)\\]", Regexes.Options);
            public static readonly Regex EmptyQuoteEndBbCode = new Regex("\\[/quote(\\s*)\\]", Regexes.Options);
        }
    }
}