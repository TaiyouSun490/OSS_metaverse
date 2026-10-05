# 同一空間・別空間とQuest LAN接続（0.7）

`SharedSpaceSession` は既存の通信プロバイダーに重ねる任意機能です。空間の位置合わせとネットワーク接続を分けています。

| モード | 座標の基準 | Meta依存 |
| --- | --- | --- |
| `SeparateSpaces` | 各端末の `separateOrigin`。同じ仮想空間の原点を自分のワールドのどこへ置くか指定 | なし |
| `Colocated` | ホストが共有し、各端末で認識した同じアンカー | `SharedAnchorProvider`。同梱のMetaアダプターではCore SDK、ネット接続、Enhanced Spatial Servicesが必要 |

「別空間」は座標配置の選択です。別のネットワークを接続するNAT越え/Relay機能ではありません。同じLANはNGO + UnityTransportの直接接続、別ネットワークは到達可能なサーバー/VPN/設定済みRelayまたは既存のFusionなどを使います。Photonを必須にはしません。

## 共通セットアップ

1. 通常のbootstrapと通信プロバイダーを設定します。自動参加と独自のHost/Join UIが重複しないよう `Join Default Room On Start` を無効にします。
2. `MetaverseRuntime` を選択し **GameObject > Taiyo Metaverse > Add Shared Space**。`runtime` と `sharedSpace` を相互に設定します。通常のworld-space `TrackedPoseSource` はそのまま使えます。
3. `runtime.JoinAsync` の完了後、ホストは `space.BeginHost(mode, anchorPlacement)`、参加者は `space.BeginClient(actualHostPeerId)` を呼びます。ホストのpeer idは通信/セッション層で確定してください。最初に届いたメッセージをホストと見なしません。
4. `space.Status` と `space.State` を表示し、Retry/Separate spaces/DisconnectをUIから呼べるようにします。`Samples~/SharedSpace` にUIボタンから呼ぶ最小例があります。
5. 実験や接触処理は `space.IsPeerReady(peer)` で双方の位置合わせを確認してから開始します。`Changed` で停止し、再試行・モード変更のあとにユーザーのStartを再要求する構成を推奨します。

`SeparateSpaces` では `separateOrigin` の位置と回転だけを使います。親のscaleを座標計算に含めず、手や身体のサイズを維持します。共通原点と各参加者の開始位置は別です。全員を同じ場所に立たせる必要はありません。

## Meta Quest 3 / 3Sの共有アンカー

Meta XR Core SDKはこのMITリポジトリへ再配布していません。利用するUnityプロジェクトへPackage Managerで追加します。

- パッケージID: `com.meta.xr.sdk.core`。アダプターの自動有効化は207以上、今回の確認対象は **207.0.0 / Unity 6000.5.9f1**。SDKのUnity最低バージョンを満たしてください。基盤パッケージのUnity 2022.3対応とMeta SDK側の対応は別です。
- XR Plug-in ManagementでAndroid OpenXRを有効にし、**Meta Quest Support** とSDKの **Meta XR Feature** を有効にします。
- XRリグに `OVRManager` を1つ配置します。独自リグでも利用でき、`OVRCameraRig` への置き換えは不要です。正しい追跡済みカメラだけを `MainCamera` にし、tracking originをリグと揃えます（Quest実験ではStage）。
- `MetaSharedAnchorProvider` を追加し、`space.anchorProvider` に指定します。
- Metaの `OVRProjectConfig` で **Anchor Support = Enabled**、**Shared Anchor Support = Supported** を設定します。手追跡を使うアプリでは **Hand Tracking Support = Controllers And Hands** なども有効にします。SDKのmanifest生成処理はこの設定を読み、無効な機能の権限を削除します。OpenXR Featureの有効化や、早いタイミングでのmanifest手動追記だけでは不十分です。
- Android manifestへ `com.oculus.permission.USE_ANCHOR_API` と `com.oculus.permission.IMPORT_EXPORT_IOT_MAP_DATA`、`android.permission.INTERNET` を追加します。
- 両端末で **設定 → プライバシーと安全 → デバイスのアクセス許可 → 高度な空間サービス** を有効にし、Metaサービスへ接続できるようにします。

同じ部屋でホストが作成・保存・グループ共有し、そのgroup UUIDとanchor UUIDを既存の通信で渡します。参加者は指定されたアンカーをロード・認識して同じ基準座標を使います。**BluetoothによるColocation Discoveryは使用しません**。セッション検索は下記のLAN探索または選んだ通信方式に任せます。パススルー表示はこの実装の必須条件ではありません。

ホストの作成・保存・共有、参加者のロード・認識の失敗をUIへ返します。初期化には45秒の上限があり、認識の喪失はFailedへ移行します。自動で別空間へ切り替えません。空間サービスが無効なら設定を確認してRetry、同じ部屋ではない場合はホストがSeparate spacesへ切り替えます。

生成したAPKの最終manifestにも上記の権限が残っていることを確認してください。独自の `IPostGenerateGradleAndroidProject` を使う場合、SDK 207の `OVRGradleGeneration` はcallback order **99999**で動作します。設定を整えた上で、アプリ側の最終検査や権限方針の適用をそれより後に置きます。ビルド成功だけでは権限の保持を確認したことになりません。

アンカーのTransformはSDKの追跡に従って更新されます。送受信の座標は毎回そのフレームを使い、XR Origin自体は動かしません。実験で必要なミリ・センチ単位の登録精度は、同じ物理的目印で双方から確認してください。認識成功だけでは精度を保証しません。

## アプリ独自の手・身体データ

`MetaverseRuntime.sharedSpace` が設定されている場合、通常の姿勢を自動的にsession座標へ変換し、新しいpose channel 4で送ります。未設定なら従来どおりです。

独自データでは `SpaceCoordinates.ToLocal(space.Frame, worldPose)` / `ToWorld` を使い、`SpacePacket.Wrap(space.Epoch, payload)` / `TryUnwrap` で包みます。別の位置合わせから遅れて届いたデータはepoch不一致で拒否されます。指のlocalRotationや身体の大きさは変更しません。

空間の制御はアプリ用channel **80**（Inspectorで変更可）。利用中のアプリchannelと衝突させないでください。ホストは0.5秒ごとに状態を再送するため途中参加にも対応します。ready通知が3秒途絶えると `IsPeerReady` はfalseになります。transportの暗号化・認証・ルームアクセス制御は元のプロバイダー/アプリの責務です。

## LAN探索

`LanRoomDiscovery` はSDK非依存のUDP探索です。デフォルトport **47778**、広告先のゲームport **7777**。ホストは `advertise=true` / `advertisedPort` / `sessionCode` を設定、参加者は `Search()`、表示した `Hosts` から接続先を選びます。探索だけでは接続・実験開始しません。

Androidには `android.permission.CHANGE_WIFI_MULTICAST_STATE` が必要です。Wi-Fiの端末間通信やブロードキャストが禁止されている場合に備え、IP手入力を用意してください。`sessionCode` は公開されるルーム識別子です。パスワードや認証tokenを入れないでください。検索nonce、packetサイズ、ホスト数、期限に上限を設けています。

## 互換性と検証

- NGOのCustomMessagingManager登録をStartHost/StartClient後へ移動し、起動失敗/切断から復帰できるようにしました。
- NGO中継時のReliable指定を維持するため、wire名は `taiyo.metaverse.packet.v2` へ更新しました。同室のNGOクライアントはすべて **0.7以上へ揃えてください**。旧NGO v1とは通信しません。
- 自動テスト: 座標の回転・平行移動・長さ維持、packet slice/epoch拒否、ホスト権限、別空間handshake、アンカー失敗/再試行/追跡喪失/モード切替/キャンセル。
- Metaクラウドでの2台共有、装着中の位置精度、ネット切断と再認識は実機での確認項目です。モックの成功を実機成功として扱いません。

参考: [Meta Shared Spatial Anchors](https://developers.meta.com/vr/documentation/unity/unity-shared-spatial-anchors/)、[Meta公式Unity sample](https://github.com/oculus-samples/Unity-SharedSpatialAnchors)。
