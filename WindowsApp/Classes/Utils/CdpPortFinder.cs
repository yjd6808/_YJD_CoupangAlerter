/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 실행 중인 엣지/크롬 프로세스의 명령줄에서 --remote-debugging-port 값을 읽어
 * CDP 포트를 자동 감지한다.
 * 
 * - 같은 브라우저 인스턴스가 여러 개면 가장 최근에 시작한 것을 사용한다.
 * - --remote-debugging-port=0(랜덤 포트)이면 --user-data-dir 폴더의
 *   DevToolsActivePort 파일 첫 줄에서 실제 포트를 읽는다.
 * - 감지 실패 시 0을 반환한다.
 * * * * * * * * * * * * *
 */

using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using RequestApi.Crawl;

namespace WindowsApp.Classes.Utils
{
    public static class CdpPortFinder
    {
        public static int FindPort(CrawlBrowser browser)
        {
            string processName = browser == CrawlBrowser.Chrome ? "chrome.exe" : "msedge.exe";
            int foundPort = 0;
            DateTime foundStartTime = DateTime.MinValue;

            try
            {
                string query = $"SELECT ProcessId, CommandLine, CreationDate FROM Win32_Process WHERE Name = '{processName}'";

                using (var searcher = new ManagementObjectSearcher(query))
                using (var processes = searcher.Get())
                {
                    foreach (ManagementObject process in processes)
                    {
                        using (process)
                        {
                            try
                            {
                                string? commandLine = process["CommandLine"] as string;
                                if (string.IsNullOrEmpty(commandLine))
                                    continue;

                                Match portMatch = Regex.Match(commandLine, @"--remote-debugging-port=(\d+)");
                                if (!portMatch.Success)
                                    continue;

                                int port = int.Parse(portMatch.Groups[1].Value);
                                if (port == 0)
                                {
                                    port = ReadDevToolsActivePort(commandLine);
                                    if (port <= 0)
                                        continue;
                                }

                                DateTime startTime = TryGetStartTime(process);
                                if (foundPort == 0 || startTime > foundStartTime)
                                {
                                    foundPort = port;
                                    foundStartTime = startTime;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            return foundPort;
        }

        private static DateTime TryGetStartTime(ManagementObject process)
        {
            try
            {
                string? creationDate = process["CreationDate"] as string;
                return creationDate == null ? DateTime.MinValue : ManagementDateTimeConverter.ToDateTime(creationDate);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        // --remote-debugging-port=0(랜덤 포트)인 경우
        // --user-data-dir 폴더의 DevToolsActivePort 파일 첫 줄에서 실제 포트를 읽는다.
        private static int ReadDevToolsActivePort(string commandLine)
        {
            try
            {
                Match dirMatch = Regex.Match(commandLine, "--user-data-dir=(?:\"([^\"]+)\"|(\\S+))");
                if (!dirMatch.Success)
                    return 0;

                string profileDir = dirMatch.Groups[1].Success ? dirMatch.Groups[1].Value : dirMatch.Groups[2].Value;
                string activePortPath = Path.Combine(profileDir, "DevToolsActivePort");
                if (!File.Exists(activePortPath))
                    return 0;

                string? firstLine = File.ReadLines(activePortPath).FirstOrDefault();
                return int.TryParse(firstLine, out int port) ? port : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
