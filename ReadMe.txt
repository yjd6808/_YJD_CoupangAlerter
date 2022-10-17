<쿠팡 알림이>
주식 갤러리를 주기적으로 크롤링해서 특정 사람 또는 특정 내용의 글을 발견하면 알려주는 프로그램
스마트폰과 윈도우용 둘 다 개발예정 (바깥에 있을 때 급하게 매매해야할 수도 있으므로)

 - 에펨코리아 주식 갤러리
 - 디시인사이드 미국 주식 갤러리

<프로젝트 종류>
AndroidApp: 안드로이드 애플리케이션 개발을 위한 자마린 폼 안드로이드 프로젝트
WindowsApp:  윈도우용 어플리케이션 개발을 위한 WPF 프로젝트
RequestApi: AndroidApp과 WindowsApp에서 사용할 .Net Standard 2.0 라이브러리 (핵심 기능을 여기서 구현)

<사용한 라이브러리>
[RequestApi]
1. HtmlAgilityPack: 다운받은 문자열로부터 DOM을 생성하기 위한 도구
2. Newtonsoft.Json: 설정 파일 저장을 위함

[AndroidApp]
1. Xamarin.Essential: Browser.OpenAsync() 함수 쓸려고 추가함
2. Xamarin.CommunityToolkit: Tabview 컨트롤을 사용하기 위함

<빌드 방법>
1. https://blog.naver.com/wjdeh313/222898764446 여기나온데로 비주얼 스튜디오 설치
 2.1 윈도우 어플리케이션을 실행하고 싶으면 WindowsApp 프로젝트 선택 후 실행
 2.2 안드로이드 어플리케이션을 실행하고 싶으면 AndroidApp.Android 프로젝트 선택 후 실행