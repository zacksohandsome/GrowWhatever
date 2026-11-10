# M5 音效來源與內容版本審查

審查日期：2026-11-10；範圍為本次新增的八個音效，未修改使用者提供的原始內容。

方法：依使用者提供的檔案與指定用途核對來源，記錄 SHA-256；Unity 已成功載入全部八檔且時長皆大於零，場景操作與獨立 Player 試玩已使用這些音效。實際索引另由安全掃描器搜尋可讀字串。此為來源與版本審查，未宣稱逐檔人工聽辨或解析所有壓縮 metadata。

WAV 為使用者先前專案的 AI 生成音效；MP3 由使用者取自効果音ラボ按鈕／系統音頁。來源與官方條款連結、用途及限制見 [素材來源](../design/asset-sources.md)。未取得 WAV 生成模型／原提示詞，以及重新命名 MP3 的原始曲目編號，無法逐位元組對照遠端原始檔。素材不是認證或個人資料來源；本次來源核對未發現疑似機敏資料。這不代表全面法律或隱藏資料保證。

| 檔案 | SHA-256 |
| --- | --- |
| `Assets/Audio/SFX/buyOrSell.wav` | `76feb13eef52d65a916702678db02bce70ad062498c09c717636c699732cdda3` |
| `Assets/Audio/SFX/click.wav` | `3e35b35163c9a9c55e0ecdc573c77b235769d078420449a86abf7332c33b0db4` |
| `Assets/Audio/SFX/harvest.wav` | `bc6890e28b0ddf6abab69dd00afa51d77fb878190720920d92ba8e15b5e91793` |
| `Assets/Audio/SFX/MissionClear.mp3` | `edfdb7c167549b4be1ac65dcde29bdf738db4323731760b00c2b84801803bf66` |
| `Assets/Audio/SFX/ResearchSuccess.mp3` | `81eb90acbb5145471eb6a28ee8f3121a96348d0826f438ad364fa58c9eebc091` |
| `Assets/Audio/SFX/sowing.mp3` | `5a286541eefafe4b57d1e3ba8de4839ddd1a241b47c05740bee31cd0ca03e5eb` |
| `Assets/Audio/SFX/UnableOperate.wav` | `933d5d87b49eb81827159a5b064d0dd22570916078438bbf9348fbfb1eff5aef` |
| `Assets/Audio/SFX/UnlockOrLevelUp.wav` | `4a2bcecc930804191ad825cc5b202fa98dce3ee96b1a373d89e11ebee116e37f` |

既有圖集、LiberationSans 與 EmojiOne 的 SHA-256 已重新比對，均與 [M0／M1 素材審查](material-review-2026-11-09.md) 相同，沿用該版本的畫面／來源審查。檔案內容變更後須重新審查。
