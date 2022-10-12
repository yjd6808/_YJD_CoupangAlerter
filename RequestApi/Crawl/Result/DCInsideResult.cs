using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class DCInsideResult : CrawlResult
    {
        public long No { get; }                 // 번호
        public string Header { get; }           // 말머리

        // ======================================================

        public bool Anonymous { get; }          // 익명 여부

        public string UserId { get; }          // 아이디 (익명이 아닌 회원가입한 유저인 경우)

        public DCInsideResult(long no, string header, string url, string title, string name, DateTime writeDate, long viewCount, long recommend, bool anonymous = false, string userId = "") : 
            base(url, title, name, writeDate, viewCount, recommend)
        {
            No = no;
            Header = header;
            Anonymous = anonymous;
            UserId = userId;
        }
    }
}
