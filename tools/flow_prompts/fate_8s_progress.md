# 미래 보기 영상 8초판 — 진행표

예약 작업 `tod-fate-8s-daily`가 매일 14:40에 이 표를 읽고 다음 2개를 만듭니다. 끝난 줄은 `[x]`로 바꾸고 날짜를 적습니다.

- Flow 프로젝트: "11월 24 - 20:02" (https://flow.google.com/project/9e08cba2-47f3-4f84-885f-a00cabb4fad3)
- 설정: 동영상, 소재(텍스트), 16:9, **Veo 3.1 - Lite**, x1, 720p, 8초 = 10크레딧. 크레딧은 매일 오후 2:26에 참(하루 약 20).
- 프롬프트: `tools/flow_prompts/fate_8s.txt`의 `[COMMON]` 한 단락 + 그 죄종 단락(색 줄 + Beat 1~3)을 한 덩어리로.
- 받은 파일: `Assets/Video/fate_<죄종>.mp4`를 **내용만** 덮어씀(`.meta` 그대로 → guid 유지, 씬 연결 그대로). 옛 파일은 먼저 `art_src/video_v1/fate_<죄종>.mp4`로 복사.

## 순서
- [ ] 1일차: wrath
- [ ] 1일차: sloth
- [ ] 2일차: envy
- [ ] 2일차: greed
- [ ] 3일차: gluttony
- [ ] 3일차: lust
- [ ] 4일차: pride

## 마무리 (7개 다 바뀐 날)
- [x] (11-25 미리 함) 영상은 제 길이 그대로 1배속으로 돌고, 끝나자마자 돌아옴(`endHold` 0). `videoTime`은 바꿀 필요 없음(영상이 없을 때만 씀).
- [ ] 플레이로 미래 보기 한 번 확인(영상 8초가 끝까지 돌고 바로 돌아오는지)
- [ ] HANDOFF.md 23차-fate-8s 절 갱신, 예약 작업 끄기

## 기록
