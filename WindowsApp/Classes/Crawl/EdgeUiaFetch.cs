/* * * * * * * * * * * * *
 * 열려 있는 Edge의 FM 검색 탭을 UIA로 읽어
 * FMKoreaCrawl 파서가 그대로 파싱하는 HTML로 변환한다.
 *
 * 전부 백그라운드 UIA 동작이라 포커스를 뺏지 않고 새 창도 안 뜬다.
 * (PowerShell + UIAutomationClient 사전 검증 완료: 새로고침 Invoke, 행 추출)
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;

namespace WindowsApp.Classes.Crawl
{
    public static class EdgeUiaFetch
    {
        private class Row
        {
            public string Category;
            public string Title;
            public string Href;
            public string Author;
            public string Date;
            public string Views;
            public string Recommends;
        }

        public static string Fetch(Uri crawlUrl)
        {
            string keyword = ExtractSearchKeyword(crawlUrl);

            AutomationElement win = FindFmWindow(keyword);
            if (win == null)
                throw new Exception("Edge에 FM 검색 탭이 없습니다. Edge에서 FM 검색 결과 탭을 열어두세요.");

            string address = ReadAddressBar(win);
            if (address != null && !(address.Contains("fmkorea.com") && address.Contains("search")))
                throw new Exception("Edge의 FM 탭이 검색 페이지가 아닙니다. FM 검색 결과 탭을 열어두세요.");

            TryRefresh(win);
            Thread.Sleep(6000);

            // 내비게이션 후 엘리먼트가 무효화될 수 있어서 다시 찾는다.
            win = FindFmWindow(keyword);
            if (win == null)
                throw new Exception("Edge FM 창을 다시 찾지 못했습니다.");

            List<Row> rows = ReadRows(win);
            if (rows.Count == 0)
                throw new Exception("FM 검색 결과 행을 찾지 못했습니다. 탭이 검색 결과 페이지인지 확인하세요.");

            return BuildHtml(rows);
        }

        private static string ExtractSearchKeyword(Uri url)
        {
            try
            {
                string query = url.Query;
                if (string.IsNullOrEmpty(query))
                    return null;

                foreach (string pair in query.TrimStart('?').Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq < 0)
                        continue;

                    if (pair.Substring(0, eq) == "search_keyword")
                        return Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                }
            }
            catch { }

            return null;
        }

        private static AutomationElement FindFmWindow(string keyword)
        {
            try
            {
                AutomationElement root = AutomationElement.RootElement;
                var winCond = new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Window);
                AutomationElementCollection wins = root.FindAll(TreeScope.Children, winCond);

                AutomationElement fallback = null;

                foreach (AutomationElement w in wins)
                {
                    string name;
                    try { name = w.Current.Name; }
                    catch { continue; }

                    if (string.IsNullOrEmpty(name) || !name.Contains("에펨코리아"))
                        continue;

                    if (!string.IsNullOrEmpty(keyword) && name.Contains(keyword))
                        return w;

                    if (fallback == null)
                        fallback = w;
                }

                return fallback;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadAddressBar(AutomationElement win)
        {
            try
            {
                var edits = win.FindAll(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Edit));

                foreach (AutomationElement edit in edits)
                {
                    string name;
                    try { name = edit.Current.Name; }
                    catch { continue; }

                    if (name != null && name.Contains("주소"))
                    {
                        var valuePattern = edit.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                        return valuePattern?.Current.Value;
                    }
                }
            }
            catch { }

            return null;
        }

        private static void TryRefresh(AutomationElement win)
        {
            try
            {
                var btn = win.FindFirst(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.NameProperty, "새로 고침"));
                if (btn == null)
                    return;

                var invoke = btn.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                invoke?.Invoke();
            }
            catch { }
        }

        private static List<Row> ReadRows(AutomationElement win)
        {
            var rows = new List<Row>();

            try
            {
                AutomationElement scope = FindDocument(win) ?? win;

                var table = scope.FindFirst(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Table));
                if (table == null)
                    return rows;

                AutomationElementCollection kids = table.FindAll(
                    TreeScope.Children, Condition.TrueCondition);

                foreach (AutomationElement kid in kids)
                {
                    ControlType ct;
                    try { ct = kid.Current.ControlType; }
                    catch { continue; }

                    if (ct != ControlType.DataItem && ct != ControlType.Custom)
                        continue;

                    Row row = ReadRow(kid);
                    if (row != null)
                        rows.Add(row);
                }
            }
            catch { }

            return rows;
        }

        private static AutomationElement FindDocument(AutomationElement win)
        {
            try
            {
                return win.FindFirst(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Document));
            }
            catch
            {
                return null;
            }
        }

        private static Row ReadRow(AutomationElement rowEl)
        {
            try
            {
                AutomationElementCollection cells = rowEl.FindAll(
                    TreeScope.Children, Condition.TrueCondition);

                if (cells.Count < 6)
                    return null;

                string author = SafeName(cells[2]);
                if (string.IsNullOrWhiteSpace(author) || author == "글쓴이")
                    return null; // 헤더 행이거나 빈 행

                string href = FindFirstLinkHref(cells[1]);
                if (string.IsNullOrEmpty(href))
                    return null; // postId를 뽑을 수 없는 행은 제외

                return new Row
                {
                    Category = SafeName(cells[0]),
                    Title = SafeName(cells[1]),
                    Href = href,
                    Author = NormalizeAuthor(author),
                    Date = SafeName(cells[3]),
                    Views = NormalizeCount(SafeName(cells[4])),
                    Recommends = SafeName(cells[5]),
                };
            }
            catch
            {
                return null;
            }
        }

        private static string SafeName(AutomationElement el)
        {
            try { return (el.Current.Name ?? "").Trim(); }
            catch { return ""; }
        }

        private static string FindFirstLinkHref(AutomationElement cell)
        {
            try
            {
                var link = cell.FindFirst(TreeScope.Descendants, new PropertyCondition(
                    AutomationElement.ControlTypeProperty, ControlType.Hyperlink));
                if (link == null)
                    return null;

                var valuePattern = link.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                return valuePattern?.Current.Value;
            }
            catch
            {
                return null;
            }
        }

        // "[레벨:35] 노라무" -> "노라무" (완전 일치 매칭도 동작하도록)
        private static string NormalizeAuthor(string author)
        {
            if (string.IsNullOrEmpty(author))
                return author;

            return Regex.Replace(author, @"^\[레벨:\d+\]\s*", "");
        }

        // "9만" -> "90000", "3천" -> "3000" (조회수 단위 복원)
        private static string NormalizeCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            Match m = Regex.Match(text.Trim(), @"^([\d.]+)만$");
            if (m.Success && double.TryParse(m.Groups[1].Value, out double man))
                return ((long)(man * 10000)).ToString();

            m = Regex.Match(text.Trim(), @"^([\d.]+)천$");
            if (m.Success && double.TryParse(m.Groups[1].Value, out double chun))
                return ((long)(chun * 1000)).ToString();

            return text;
        }

        private static string BuildHtml(List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<table class=\"bd_lst\"><tbody>");

            foreach (Row r in rows)
            {
                sb.Append("<tr>");
                sb.Append("<td class=\"cate\">").Append(SecurityElement.Escape(r.Category)).Append("</td>");
                sb.Append("<td class=\"title\"><a href=\"").Append(SecurityElement.Escape(r.Href)).Append("\">")
                    .Append(SecurityElement.Escape(r.Title)).Append("</a></td>");
                sb.Append("<td class=\"author\">").Append(SecurityElement.Escape(r.Author)).Append("</td>");
                sb.Append("<td class=\"time\">").Append(SecurityElement.Escape(r.Date)).Append("</td>");
                sb.Append("<td class=\"m_no\">").Append(SecurityElement.Escape(r.Views)).Append("</td>");
                sb.Append("<td class=\"m_no_voted\">").Append(SecurityElement.Escape(r.Recommends)).Append("</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }
    }
}
