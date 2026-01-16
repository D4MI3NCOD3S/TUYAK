# TUYAK - DUR 기술 검증 프로젝트

개인투약이력 조회 시스템 (DUR) 기술 검증 및 비교 프로젝트

## 프로젝트 개요

이 프로젝트는 건강보험심사평가원(HIRA)의 DUR(Drug Utilization Review) 시스템과 통신하는 두 가지 방식을 비교 검증합니다:

1. **COM 방식** - 기존 레거시 DLL/OCX 호출 방식
2. **소켓 통신 방식** - 직접 TCP/IP 소켓으로 브로커 서버와 통신

## 기술 스택

- .NET 8.0
- ASP.NET Core MVC
- C# 12.0
- Bootstrap 5
- jQuery

## 주요 기능

### 1. COM 방식 테스트
- CheckMediHistory
- CheckMediHistoryList
- CheckMediDataList

### 2. 소켓 통신 - 일반 조회 (Code 6)
- Broker 모드 ('B'): 팝업 URL 반환
- Server 모드 ('S'): 텍스트 데이터 반환 (DB 저장 가능)

### 3. 소켓 통신 - 응급 조회 (Code 3)
- 인증번호 생략 가능
- Broker/Server 모드 지원

## 설정 방법

### 1. COM 참조 등록

COM DLL을 관리자 권한으로 등록:

```cmd
regsvr32 "C:\path\to\HiraDur.dll"
```

### 2. 프로젝트 설정

- 플랫폼 대상: **x86** (32비트)
- COM Hosting 활성화됨

### 3. 실행

```bash
dotnet run
```

## 프로젝트 구조

```
WebApplication1/
├── Controllers/
│   └── HomeController.cs          # 메인 컨트롤러
├── Models/
│   └── HiraService.cs              # DUR 통신 서비스
├── Views/
│   └── Home/
│       └── Index.cshtml            # 테스트 UI
├── Program.cs                      # 앱 진입점
└── WebApplication1.csproj          # 프로젝트 파일
```

## 주요 클래스

### HiraService

DUR 시스템과 통신하는 핵심 서비스 클래스

- `RunComMethodLegacy()`: COM 방식으로 DUR 조회
- `RunSocketTestAsync()`: 소켓 방식으로 DUR 조회
- `RequestAuthAsync()`: 인증번호 요청
- `ExtractAuthNumber()`: 응답에서 인증번호 추출

### DurResponse

DUR 조회 결과를 담는 응답 모델

- `Success`: 성공 여부
- `Message`: 응답 메시지
- `ResultType`: 결과 타입 (URL/DATA/ERROR)
- `Content`: 응답 내용
- `DataList`: 데이터 목록

## 라이선스

MIT License

## 작성자

원복 이 (hwfrzy@gmail.com)
