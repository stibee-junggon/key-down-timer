# 게임 타이머 (Game Timer)

게임 위에 띄워 두는 작은 Windows 오버레이 타이머입니다. 지정한 키를 누르면 해당 타이머가 시작되고, 시간이 다 되면 짧은 "띵" 소리로 알려줍니다.

## 기능

- 타이머 여러 개 등록: 키 / 이름(선택) / 시간(mm:ss)
- 전역 키 감지: 게임 창에 포커스가 있어도 동작하며, 키 입력은 차단하지 않고 게임에 그대로 전달됩니다.
- 같은 키를 다시 누르면 해당 타이머가 처음부터 다시 시작됩니다. (누르고 있는 동안의 반복 입력은 무시)
- 남은 10초 이하는 주황색, 종료 시 행 깜빡임 + 부드러운 알림음
- 오버레이: 반투명, 항상 위, 드래그로 이동(위치 저장), 포커스를 뺏지 않음
- 설정 창(⚙): 타이머 추가/삭제, 키 지정, 소리 크기·미리듣기, 오버레이 투명도
- 설정 파일: `%APPDATA%\GameTimer\settings.json`

## 다운로드

[Releases](https://github.com/stibee-junggon/key-down-timer/releases/latest)에서 `GameTimer.exe`를 받습니다.

> 서명되지 않은 exe라 처음 실행할 때 Windows SmartScreen 경고가 뜰 수 있습니다. "추가 정보 → 실행"을 누르면 됩니다.

## Windows에서 실행

1. `GameTimer.exe` 하나만 원하는 폴더에 두고 실행합니다. (.NET 설치 불필요)
2. 오버레이에 마우스를 올리면 나타나는 ⚙ 버튼으로 타이머를 추가합니다.
3. 종료는 오버레이의 ✕ 버튼.

> 메이플스토리 등 게임은 **창 모드(또는 창 모드 전체화면)** 에서만 오버레이가 게임 위에 보입니다. 전체화면(독점) 모드에서는 가려집니다.
>
> 게임을 관리자 권한으로 실행하는 경우, 키 감지를 위해 이 앱도 관리자 권한으로 실행해야 할 수 있습니다.

## 빌드

.NET 8 SDK가 필요합니다. (macOS/Linux에서도 `EnableWindowsTargeting` 덕분에 빌드 가능, 실행은 Windows에서만)

```sh
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

결과물: `bin/Release/net8.0-windows/win-x64/publish/GameTimer.exe`
