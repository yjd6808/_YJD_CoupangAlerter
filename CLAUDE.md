# Claude Instructions

## Git Commit
코드 작업 후 항상 `git add .` → `git commit -m "..."` → `git push` 순서로 저장한다.

## Commit Message Format
```
N. 변경사항 대제목
 - 변경1
 - 변경2
 - 변경3
```
- 디스크립션 앞에 넘버링을 포함한다.
- 주요 변경사항을 대제목으로 작성하고, 상세 변경내역은 bullet list로 나열한다.

## Documentation
- 기능 변경 시 docs/ 문서를 함께 갱신한다.
  - docs/overview.md: 프로젝트 개요, 설정 파일 키, 빌드/실행, 브라우저 사전 준비
  - docs/edge-flow.md: 엣지/크롬 CDP 크롤링 흐름 (WebFetch, 창 모드, 포트 자동 감지)
  - docs/files.md: 파일별 간략 분석
- md 문서와 스크립트는 UTF-8 with BOM + CRLF로 작성한다.

## Build & Test
- 빌드: `dotnet build WindowsApp\WindowsApp.csproj -c Release` (WebFetch도 함께 빌드되어 출력 폴더로 복사됨)
- 크롤링 테스트 전 실행 중인 엣지(9222) 또는 크롬(9223)이 `--remote-debugging-port` + `--user-data-dir` 옵션으로 필요하다.
