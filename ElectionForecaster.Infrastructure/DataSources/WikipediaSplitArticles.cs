using System.Text.RegularExpressions;

namespace ElectionForecaster.Infrastructure.DataSources;

/// <summary>
/// Follows a state House article that Wikipedia has split by district range. Once a state's page
/// grows too large, editors move the districts out into sub-articles ("...elections in California
/// (districts 1–26)") and leave the parent with a {{main|…}} link per range. Each sub-article keeps
/// the usual "District N" sections, so callers only need to know which page to read.
/// </summary>
public static partial class WikipediaSplitArticles
{
    /// <summary>The title of the sub-article covering <paramref name="district"/>, or null.</summary>
    public static string? SubArticleForDistrict(string parentWikitext, int district)
    {
        foreach (Match link in MainLinkRegex().Matches(parentWikitext))
        {
            foreach (var target in link.Groups[1].Value.Split('|'))
            {
                var title = target.Trim();
                var range = DistrictRangeRegex().Match(title);
                if (range.Success &&
                    int.Parse(range.Groups[1].Value) <= district &&
                    district <= int.Parse(range.Groups[2].Value))
                    return title;
            }
        }
        return null;
    }

    [GeneratedRegex(@"\{\{\s*main\s*\|([^}]+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex MainLinkRegex();

    [GeneratedRegex(@"\(districts?\s+(\d+)\s*[–—-]\s*(\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex DistrictRangeRegex();
}
