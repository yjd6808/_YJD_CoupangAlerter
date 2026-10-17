# 프로젝트 개요

주식 커뮤니티(FMKorea 주식 게시판, DCInside 미국주식 갤러리)를 주기적으로 크롤링해,
특정 닉네임 또는 제목의 글을 발견하면 알림을 주는 프로그램이다.
윈도우(WPF)와 안드로이드(Xamarin.Forms) 버전을 함께 개발 중이며, 현재 실사용 가능한 것은 윈도우 버전이다.

## 프로젝트 구성 (4개)

| 프로젝트 | 종류 | 역할 |
|---|---|---|
| RequestApi | netstandard2.0 라이브러리 | 크롤링·매칭·설정 저장 등 핵심 기능. WindowsApp/AndroidApp 공용 |
| WebFetch | net6.0 콘솔 앱 | 실행 중인 엣지/크롬(CDP)에 접속해 HTML을 추출하는 외부 도구. Microsoft.Playwright 사용 |
| WindowsApp | net6.0-windows WPF 앱 | 윈도우 UI. RequestApi 사용, WebFetch.exe를 빌드 후 출력 폴더로 복사 |
| AndroidApp / AndroidApp.Android | Xamarin.Forms | 안드로이드 앱(개발 진행 중). RequestApi 재사용 |

## 전체 동작 흐름

1. WindowsApp에서 크롤링 시작
2. CrawlTaskManager의 사이트별 워커 스레드(CrawlTaskWorker)가 CrawlTask를 크롤링 주기마다 실행
3. CrawlTask.Do() → AbstractCrawl.TryCrawl(브라우저 창 모드, CDP 포트) → WebFetch.exe 실행 → HTML 획득
4. HtmlAgilityPack으로 DOM 파싱 → CrawlResult 목록 생성
5. CrawlTask가 매칭 판정
   - 매칭 대상: 닉네임 또는 제목
   - 문자열 규칙: 일부 포함 또는 정확히 일치
   - 최근 N분(작업별 MatchingRangeMinute) 이내 작성 글만 유효
   - 이미 매칭된 글(PostId)은 제외
6. 매칭 시 알림(점멸/소리/음성)과 매칭 목록 기록
   - 첫 크롤링 사이클에서 발견된 글은 알림이 억제된다(시작 직후 과거 글 폭탄 방지)

## 크롤링 대상과 방식

- FMKorea: `search.php?mid=stock` 검색 페이지 사용 (검색 대상 닉네임/제목, 카테고리·페이지 지정 가능)
- DCInside: `mgallery/board/lists?id=stockus` (전체글/개념글, 페이지 지정 가능)
- HTML 획득은 모두 WebFetch.exe 경유(실행 중 엣지/크롬 CDP)로 단일화되어 있다. 상세: docs/edge-flow.md

## 설정 파일

실행 폴더 하위 `config/tasks.json`에 저장된다.

| 키 | 의미 |
|---|---|
| crawls[] | 작업 목록 (taskName, crawlType, matchString, stringMatchRule, matchType, matchingRangeMinute, page, boardType, searchOption, searchContent) |
| dcCrawlDelay / fmCrawlDelay | 사이트별 크롤링 주기(ms) |
| dcCrawlBlocked / fmCrawlBlocked | 사이트 크롤링 끄기/켜기 |
| edgeWindowMode | 크롤링 시 브라우저 페이지를 여는 방식 (0=새 탭, 1=새 창, 2=새 창 숨김) |
| crawlBrowser | 크롤링에 사용할 브라우저 (0=엣지, 1=크롬) |
| cdpPortAuto | CDP 포트 자동 감지 사용 여부 (기본 true) |
| edgeCdpPort / chromeCdpPort | 수동 지정 CDP 포트 (기본 엣지 9222 / 크롬 9223) |
| maxMatchedDisplayCount | 매칭 결과 표시 최대 개수 |
| crawlTaskUidSeq | 작업 고유번호 시퀀스 |

- 구버전 파일의 `crawlMethod`, `fmLoginId`, `fmLoginPw` 키는 로드 시 무시되고 저장되지 않는다.
- 매칭 기록은 메모리에만 보관하며 1일 경과 시 만료된다.

## 빌드와 실행

- 요구 사항: .NET SDK (net6.0-windows 타깃, 현재 EOL 경고 있음)
- 빌드: `dotnet build WindowsApp\WindowsApp.csproj -c Release`
  - WebFetch도 함께 빌드되어 WindowsApp 출력 폴더로 복사된다 (CopyWebFetchOutput 타깃, WebFetch.exe + `.playwright` node 포함)
- 실행: `WindowsApp\bin\Release\net6.0-windows\WindowsApp.exe`
- 크롤링 전에 실행 중인 엣지(또는 크롬)가 필요하다(아래).

## 브라우저 사전 준비 (필수)

엣지:

```
msedge.exe --user-data-dir="%LOCALAPPDATA%\Edge-CDP" --profile-directory=Default --remote-debugging-port=9222
```

크롬:

```
chrome.exe --user-data-dir="%LOCALAPPDATA%\Chrome-CDP" --profile-directory=Default --remote-debugging-port=9223
```

- 사용할 브라우저는 프로그램 설정의 '크롤링 브라우저'에서 선택한다(기본 엣지).
- Chromium 136 이후 버전은 기본 프로필에서 원격 디버깅을 차단하므로 `--user-data-dir`로 전용 프로필을 지정해야 한다.
- FMKorea 크롤링은 로그인이 필요하므로 사용할 브라우저 프로필에서 로그인해 둔다. 앱은 계정 비밀번호를 저장하지 않으며, 로그인된 브라우저 세션(쿠키)을 그대로 사용한다.
- 시작 버튼을 누르면 CDP 포트를 결정한다. 자동 감지가 켜져 있으면 실행 중인 브라우저 프로세스 명령줄에서 포트를 얻고, 실패하면 수동 입력한 포트를 사용한다. 이후 127.0.0.1:포트 접속 가능 여부를 검사하고, 안 되면 실행 방법을 안내한다.

## 관련 문서

- docs/edge-flow.md: 브라우저(엣지/크롬) CDP 크롤링 동작 방식
- docs/files.md: 파일별 간략 분석
- Document/: 기존 구조 다이어그램 (프로젝트/클래스/스레드 구조, PlantUML)
- category_verify.txt: FMKorea 카테고리 ID 육안 검증 기록
