# Taiyo Metaverse Kit

Unity 2022.3 LTS向けの、特定ベンダーにロックインされないメタバース基盤です。UPMパッケージとして導入でき、通信・音声を`ScriptableObject`プロバイダーで交換し、XRとデスクトップを同じ姿勢プロトコルで扱います。ライセンスはMITです。

> **Status: 0.6 / foundation.** 少人数のプロトタイプを立ち上げるための土台です。認証サーバー、UGC審査、永続化、管理画面までを含む完成サービスではありません。

## 含まれるもの

- ルーム参加、presence、peer join/leave、アプリ独自メッセージ
- ルート・頭・左右の手・hips・左右footの姿勢同期と補間
- `NetworkProvider`による通信SDKの交換
  - 依存なしのLoopback（標準）
  - Netcode for GameObjects（OSS構成の推奨）
  - Photon Fusion 2.1（Photon構成の推奨）
  - Photon PUN 2（既存プロジェクト互換）
- `VoiceProvider`による音声SDK/codecの交換
  - raw PCMの動作確認用実装
  - Photon Voice 2 + Opus（Fusion向け任意依存）
  - mute/blockと音声受信の連動
- Desktop CharacterControllerと、XR/desktopリグの自動切替
- Meta XR Movement SDKによる全身trackingとFinal IK VRIK補正（任意依存）
- SteamVR tracker FBTとmocopi Quest受信、保存可能な姿勢calibration
- Addressablesによるremote catalog、アバターprefab、world sceneの読み込み
- AddressablesビルドをローカルフォルダーまたはHTTP PUT先へ公開するEditor UI
- Bootstrap生成メニュー、Quick Start sample、EditMode tests

## 最短セットアップ

1. Unity Package Managerの **Add package from disk…** で`package.json`を選択します。
2. **Tools > Taiyo Metaverse > Create Bootstrap** を実行します。
3. desktop rigまたはXR Originに`TransformPoseSource`を追加し、root/head/handを割り当てます。
4. そのcomponentを`MetaverseRuntime.localPoseSource`へ割り当ててPlayします。

生成される設定はLoopback + PCM voiceです。Loopbackは同一Unityプロセス内で複数runtimeを試す用途で、別プロセス・実機間通信には下記アダプターを使います。

Git URLから導入する場合はPackage Managerの **Add package from git URL…** に`https://github.com/TaiyouSun490/OSS_metaverse.git`を指定します。

## 通信の切替

新規プロジェクトは、OSS中心ならNetcode for GameObjects、Photon Cloudを使うならFusion 2.1を推奨します。PUN 2はPhoton公式でもmaintenance/LTS扱いで、このpackageでは既存PUNプロジェクトとの互換アダプターとして残しています。特にFBTのような高頻度・多関節の同期では、PUN 2を主軸にせず、送信頻度、量子化、補間、interest managementを制御できる構成にしてください。

### Photon Fusion 2.1

1. Photon公式からFusion 2.1 SDKをimportし、Fusion 2用AppIdを設定します。
2. Player SettingsのScripting Define Symbolsへ`TAIYO_METAVERSE_FUSION`を追加します。
3. **Create > Taiyo Metaverse > Networking > Photon Fusion 2 Provider** を作り、configへ割り当てます。
4. Shared topology（標準）またはHost/Client topologyを選びます。

Fusion adapterは`RoomRequest.roomId`をsession名、`capacity`をPlayerCount、`visibility`をIsVisibleへ反映します。static RPCでkitのpacketを転送し、unreliable payloadは479 bytes、Fusion 2.1のReliable Large Data payloadは16,383 bytesを上限とします。アバター姿勢は205 bytes以下なのでunreliableに収まります。それ以上のデータはAddressables/CDNを使い、transportへ流さないでください。

Private sessionの`accessToken`はHost/Client topologyではFusionのConnectionTokenへ渡しますが、このadapter単体はtokenを認可しません。PlayFab Functionsや専用serverで検証を追加してください。Shared topologyではConnectionTokenが認可境界にならないため、private roomを本番運用する場合はPhoton WebHooks/Pluginを使うかHost/Client topologyを選びます。

### Netcode for GameObjects

1. `com.unity.netcode.gameobjects`と利用するtransportを導入します。
2. sceneに設定済みの`NetworkManager`を置きます。
3. **Create > Taiyo Metaverse > Networking > Netcode Provider** を作ります。
4. `MetaverseConfig`のNetwork Providerを差し替えます。
5. host側の`RoomRequest.host`を有効にします。

NGO adapterは既存`NetworkManager`を使います。接続先やRelay設定は`NetworkTransport`側の責務です。`roomId`によるdirectory/マッチメイクはバックエンド側に実装してください。

### Photon PUN 2

1. Photon PUN 2を導入し、AppIdを設定します。
2. Asset Store版ではPlayer SettingsのScripting Define Symbolsに`TAIYO_METAVERSE_PHOTON`を追加します。UPM package版は自動検出されます。
3. **Create > Taiyo Metaverse > Networking > Photon PUN Provider** を作り、configへ割り当てます。

Photonのroom名には`RoomRequest.roomId`、定員には`capacity`を使用します。
姿勢送信はkit側の`MetaverseConfig.poseSendRate`（既定20 Hz）で制御されます。PUN 2 adapterは単純な`RaiseEvent`転送であり、Fusionのsnapshot/prediction相当の機能は提供しません。

## 音声

`PcmVoiceProvider`はマイクからmono PCM16を取得し、voice channelで非信頼送信します。追加SDKなしで機能確認できますが、圧縮・暗号化・AEC・ノイズ抑制を持たないため公開サービスには不向きです。本番では`VoiceProvider`を実装して、Opus/WebRTC、Vivox、Photon Voiceなどへ置き換えてください。

Fusion構成では`FusionPhotonVoiceProvider`を収録しています。Photon Voice 2のOpus、jitter buffer、暗号化、voice activity detectionを使用し、入力muteと`SafetyService`のpeer mute/blockを反映します。

1. Fusion HubのAddonsから、Fusion 2.1と互換のPhoton Voiceを導入します。
2. Photon Dashboardで別のVoice AppIdを作り、Photon App SettingsのApp Id Voiceへ設定します。
3. Scripting Define Symbolsへ`TAIYO_METAVERSE_PHOTON_VOICE`を追加します（Fusion側のdefineも必要です）。
4. `Speaker`と`AudioSource`を持つprefabを作ります。proximity voiceでは`AudioSource.spatialBlend = 1`にします。
5. **Create > Taiyo Metaverse > Voice > Photon Voice for Fusion** を作り、speaker prefabを設定して`MetaverseConfig`へ割り当てます。

アバターの頭位置に厳密に追従するproximity voiceは、Fusionでspawnするplayer prefabへPhoton Voiceの`VoiceNetworkObject`、`Recorder`、`Speaker`を設定する公式構成を使います。この場合もSDK本体やそのsample assetは本リポジトリへ含めません。

## アバターとワールドの配信

1. avatar prefabとworld sceneをAddressableにします。
2. avatar prefabに`AvatarRig`を追加し、root/head/leftHand/rightHand targetを設定します。
3. Addressables ProfilesでRemote Build PathとRemote Load PathをCDNに合わせます。
4. **Create > Taiyo Metaverse > Content Publish Profile** を作ります。
5. **Tools > Taiyo Metaverse > Content Publisher** でBuild and Publishします。
6. remote catalog URLをsceneの`RemoteContentService`へ登録します。

公開先はフォルダーコピーとHTTP PUTに対応します。HTTP tokenはprofileに保存せず、`bearerTokenEnvironmentVariable`で指定した環境変数から読みます。S3/R2/GCSの署名APIが必要な場合は、`MetaverseContentPublisher`と同じ入力を使う専用publisherを追加してください。

アバターのaddressは`LocalUserProfile.avatarAddress`でpresenceとして共有されます。ワールドは`MetaverseRuntime.LoadWorldAsync(address)`でadditive loadされ、前のremote worldをunloadします。

## アーキテクチャ

```text
MetaverseRuntime
├─ NetworkProvider ─ Loopback / NGO / Fusion / PUN / custom
├─ VoiceProvider   ─ PCM / Opus / managed voice service
├─ TrackedPoseSource ─ Desktop camera / XR Origin / custom tracking
├─ RemoteContentService ─ Addressables catalog / avatar / world
└─ SafetyService ─ mute / block
```

transport packetのchannel 0–63はkit内部用、64–255はアプリ用です。独自機能は`SendUserMessage`と`UserMessageReceived`で追加でき、transport固有APIをgameplay codeへ漏らしません。

## 「あと必要なもの」— 公開サービスに進む順番

このpackageの次に必要なのは、見た目より先に信頼境界です。

1. **認証・認可** — guest/session token、room ACL、ban、server-authoritativeな権限
2. **モデレーション** — report、mute/blockの永続化、kick、監査log、年齢/プライバシー対応
3. **UGC安全pipeline** — upload形式検証、容量/triangle/shader制限、malware scan、審査、署名
4. **永続化** — user profile、inventory、world state、ownershipのbackend API
5. **room discovery** — lobby、invite、friend、region/latency選択、instance lifecycle
6. **スケーラビリティ** — interest management、LOD、帯域budget、server/relay、負荷試験
7. **音声の本番化** — Opus、AEC/NS/AGC、spatial audio、暗号化、話者moderation
8. **操作とアクセシビリティ** — Input System remap、snap turn、seated mode、字幕、酔い対策
9. **観測性** — crash/error、接続成功率、RTT/jitter/packet loss、content download telemetry
10. **互換性** — protocol version、content schema、段階rollout、catalog rollback、migration

`SECURITY.md`に公開前checklistがあります。

## 拡張ポイント

- 通信SDK追加: `NetworkProvider`を継承
- 音声追加: `VoiceProvider`を継承
- tracking追加: `TrackedPoseSource`を継承
- app message: channel 64以上を利用
- CDN固有upload: Editor publisherを追加

詳しくは`Documentation~/ProviderAuthoring.md`を参照してください。

## Known limitations

- NGO adapterはroom directoryを持たず、host/client接続設定は既存transportに委譲します。
- Fusion adapterはFusion 2.1 SDKを任意依存とし、room discovery UIとserver側token検証は含みません。
- Photon PUN adapterは互換用途です。PUN roomと`RaiseEvent`を使い、server-authoritative simulation、snapshot interpolation、prediction、interest managementを提供しません。
- PCM voiceは帯域効率、jitter制御、AEC/NS、暗号化を提供しません。
- avatarの骨格retargetingやIKは`AvatarRig`の外側です。
- UGCのuploadは配信ファイル公開までで、backend側の審査・署名・権限管理は別途必要です。

## License

MIT. See `LICENSE.md`.

## PlayFabアカウントとフレンド

`AccountProvider`を追加し、標準実装として`LocalAccountProvider`と`PlayFabRestAccountProvider`を収録しています。PlayFab Unity SDKへ依存せずClient REST APIを使うため、通信層のNetcode/Photonとは独立して交換できます。

1. PlayFabでTitleを作成し、Title IDを取得します。
2. **Create > Taiyo Metaverse > Accounts > PlayFab REST Provider** を作成します。
3. providerへTitle IDを設定し、`MetaverseConfig > Account Provider`へ割り当てます。
4. `MetaverseRuntime.SignInAsync`または`RegisterAsync`を呼び出します。

対応機能:

- email/password、username/passwordログイン
- 新規アカウント登録
- Custom ID guestログインとguestアカウント昇格
- 表示名更新
- フレンド一覧、追加、削除
- フレンドの表示名・avatar URL取得
- PlayFab IDと表示名のmetaverse presence連携

PlayFabのDeveloper Secret Keyはクライアントへ入れません。新規Titleではanonymous APIによるplayer作成が既定で無効なので、本番guestは信頼できるserverで作成してください。詳細は`Documentation~/PlayFabAccounts.md`を参照してください。

## Public／Privateルーム

`RoomRequest.visibility`で`Public`または`Private`を選べます。

- Public: `DiscoverPublicRoomsAsync()`の検索対象
- Private: 検索には出ず、room IDと短命な`accessToken`で参加
- Photon PUN: `RoomOptions.IsVisible`へ反映
- NGO: room directoryを持たないため、PlayFabとConnection Approvalで認可

```csharp
await runtime.JoinAsync(new RoomRequest {
    roomId = "friends-room",
    visibility = RoomVisibility.Private,
    createIfMissing = false,
    accessToken = inviteToken
});
```

非表示は認可ではありません。本番のPrivateルームはPlayFab Functionsでowner/member/inviteを確認し、短命なroom tokenを発行してtransport/server側で検証してください。詳細は`Documentation~/Rooms.md`を参照してください。

## Final IK＋Movement SDKアバター

ローカルアバターはMovement SDKで全身をretargetし、その結果をFinal IK VRIKで頭・骨盤・両手へ拘束できます。remoteアバターではMovement SDKを止め、同期されたroot/head/hand targetをVRIKで全身姿勢へ復元します。

1. Meta XR Movement SDKとFinal IKをプロジェクトへ導入します。
2. Humanoid avatarへMovement SDKのretargeterとFinal IKの`VRIK`を設定します。
3. avatarを選択して **GameObject > Taiyo Metaverse > Configure Avatar Motion** を実行します。
4. `FinalIkVrikBridge`へHMD、pelvis、左右hand targetを割り当てます。
5. 生成された`HumanoidPoseSource`を`MetaverseRuntime.localPoseSource`へ設定します。

両SDKはライセンス上このOSSへ同梱していません。未導入でもreflection bridgeによりbase packageはcompileできます。現在のMovement SDKはUnity 6000.0.66f2以上とMeta XR SDK v81以上が必要です。詳細は`Documentation~/AvatarMotion.md`を参照してください。

## SteamVR tracker／mocopi FBT

waist・左右footの3点を最小FBT構成として、trackerの装着位置とavatar targetの差をcalibrationします。位置・回転offsetはprofile keyごとに保存されます。

1. avatarを選択して **GameObject > Taiyo Metaverse > Configure Full Body Tracking** を実行します。
2. SteamVRではwaist／左右footに`SteamVR_Behaviour_Pose`を設定し、`SteamVrFullBodySource`へ割り当てます。
3. Quest単体+mocopiではSony Receiver Pluginが動かす非表示Humanoidを用意し、そのAnimatorを`MocopiHumanoidFullBodySource`へ割り当てます。
4. 正面を向いた直立姿勢で`FullBodyTrackingCalibrator.Calibrate()`を呼びます。Quick Startの`FullBodyCalibrationHud`も利用できます。

mocopiはsensorをphone appへ接続し、同一LANのQuestへ`mocopi (UDP)`を送る構成です。Unity Receiver PluginのAndroid IL2CPP/ARM64 buildをQuest appへ含めます。

新しい205-byte pose packetはhips・両足を含み、decoderは従来の121-byte packetも受信できます。ただし旧buildは新packetを読めないため、FBTを有効にするroomでは全参加者を0.5.0以上へ更新してください。

詳細は`Documentation~/FullBodyTracking.md`を参照してください。
