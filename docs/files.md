# 파일별 간략 분석

빌드 산출물(bin/obj), `.vs`, `packages`, `.git`은 제외한다.

## 솔루션 루트

| 파일 | 설명 |
|---|---|
| CoupangAlerter.sln | 4개 프로젝트 솔루션 (RequestApi, WebFetch, WindowsApp, AndroidApp) |
| CLAUDE.md | AI 작업 지침 (커밋 규칙, 문서 안내) |
| ReadMe.txt | 프로젝트 메모 |
| category_verify.txt | FMKorea 카테고리 ID 육안 검증 기록 |
| docs/ | 문서 (overview.md, edge-flow.md, files.md) |
| Document/Structures/*.puml | PlantUML 구조도 (프로젝트/클래스/스레드) |

## RequestApi (핵심 라이브러리, netstandard2.0)

WindowsApp과 AndroidApp이 공용으로 사용한다.

### Crawl/

| 파일 | 설명 |
|---|---|
| CrawlType.cs | 크롤링 대상 상수 (DCInside=0, FMKorea=1, Max=2) |
| CrawlBrowser.cs | 크롤링에 사용할 브라우저 열거형 (Edge=0, Chrome=1) |
| EdgeWindowMode.cs | 페이지를 여는 방식 열거형 (Tab=0, Window=1, Hidden=2) |
| CrawlTaskManager.cs | 작업 목록 관리, tasks.json 로드/저장, 사이트별 워커 스레드 관리, 매칭 기록 보관, 통계 |
| CrawlTaskWorker.cs | 사이트별 크롤링 주기 실행 워커 스레드 (작업 큐 + 주기 대기) |
| CrawlTask.cs | 작업 단위. Do()에서 크롤링 → 매칭 판정 → 이벤트 발행 |
| AbstractCrawl.cs | 크롤링 공통 절차. WebFetch.exe 실행, 로그인 벽 판정, 파싱 진입점 |
| DCInsideCrawl.cs | DCInside 미국주식 갤러리 URL 구성 (전체글/개념글, 페이지) 및 파싱 |
| FMKoreaCrawl.cs | FMKorea 주식 게시판 검색 URL 구성 (검색 옵션/카테고리/페이지) 및 파싱 |

### Crawl/Result/

| 파일 | 설명 |
|---|---|
| CrawlResult.cs | 결과 공통 속성 (PostId, Url, Title, Name, WriteDate, ViewCount, Recommend) |
| DCInsideCrawlResult.cs | DC 전용 속성 (말머리 Header, 익명 여부, 유저 ID) |
| FMKoreaCrawlResult.cs | FM 전용 속성 (게시판 종류, 오늘 글 여부) |
| MatchedCrawlResult.cs | 매칭된 결과 + 매칭 시각 + 첫 사이클 메시지박스 억제 플래그 |

### Utils/

| 파일 | 설명 |
|---|---|
| Logger.cs | 디버그 로그 (DEBUG 빌드에서 파일 기록) |
| ArrayIndexer.cs | 배열 인덱서 래퍼 |
| StringExtension.cs | 문자열 유틸 (숫자만 추출 등) |
| ThreadExtension.cs | 락 해제 후 Sleep하는 유틸 |
| WaitHandleExtension.cs | 락 해제 후 WaitOne하는 유틸 |

## WebFetch (HTML 추출 도구, net6.0 콘솔)

| 파일 | 설명 |
|---|---|
| Program.cs | 실행 중인 엣지/크롬에 CDP 접속 → 페이지 열기 → HTML 표준출력. 인자 `url [tab/window/hidden] [port]` |
| WebFetch.csproj | Microsoft.Playwright 참조 |

동작 방식 상세는 docs/edge-flow.md 참조.

## WindowsApp (WPF UI, net6.0-windows)

| 파일 | 설명 |
|---|---|
| Windows/MainWindow.xaml(.cs) | 메인 창. 로그, 크롤 목록, 프로그램 설정 탭, 매칭 목록, 알림(점멸/소리/음성/트레이 아이콘) |
| Classes/Utils/CdpPortFinder.cs | 실행 중인 브라우저 프로세스 명령줄에서 CDP 포트 자동 감지 (WMI) |
| Classes/Utils/MsgBox.cs | 최상위(TopMost) 메시지 박스 헬퍼 |
| App.xaml(.cs) | 앱 진입점 |
| AssemblyInfo.cs | 어셈블리 메타데이터 |
| WindowsApp.csproj | 빌드 설정. System.Management 참조, CopyWebFetchOutput 타겟(WebFetch.exe를 출력 폴더로 복사) |

## AndroidApp / AndroidApp.Android (개발 진행 중, Xamarin.Forms)

| 파일 | 설명 |
|---|---|
| AndroidApp/MainPage.xaml(.cs) | 안드로이드 UI |
| AndroidApp/Classes/Services/* | 알림/네트워크/포그라운드 서비스 인터페이스 (IAppCloser, IAppInitializer, INetConnectivityService, INotificationManager, IForegroundService) |
| AndroidApp/Classes/Utils/* | 디버그/페이지 확장 유틸 |
| AndroidApp.Android/MainActivity.cs | 액티비티 진입점 |
| AndroidApp.Android/Classes/Services/AlarmHandler.cs | 알람 처리 |
| AndroidApp.Android/Classes/Services/State/ForegroundService.cs | 포그라운드 서비스 |
| AndroidApp.Android/Classes/Services/Network/NetConnectivityService.cs | 네트워크 상태 서비스 |
