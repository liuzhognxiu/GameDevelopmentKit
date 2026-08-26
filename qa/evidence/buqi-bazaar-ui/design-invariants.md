# 坊市 UI 独立验收不变量

来源：`docs/superpowers/specs/2026-08-21-buqi-bazaar-reference-ui-interaction-flow-design.md`，不是实现常量。

| 不变量 | 当前证据 | 结论 |
|---|---|---|
| 1280x720 与 1920x1080 均可读、可操作、无重叠或截断 | 没有匹配提交的 Unity 运行态或截图 | NOT_RUN |
| 货架提供最多 10 个连续格，不折成两排 | 运行时与 Builder 均固定 8 件、4 列 2 行 | FAIL |
| 点击商品只预览，购买与金币变化均为 0 | `ShopWidget` 向卡片传入购买回调，`OfferCardWidget` 给按钮绑定回调 | FAIL |
| 悬停商品显示正式作用 | Hover trigger 与详情回调存在；未取得运行态画面 | 静态 PASS / 实机 NOT_RUN |
| 商品拖到合法棋盘位置只提交一次定点购买 | Drop 路径提交 `BuyOffer` 并携带释放格索引；未实机拖拽 | 静态 PASS / 实机 NOT_RUN |
| 棋盘器物拖到商店只提交一次出售 | Sell drop 路径提交 `SellItem`；未实机拖拽 | 静态 PASS / 实机 NOT_RUN |
| 商店打开时玩家棋盘始终可见 | 运行时创建 8 格棋盘；未取得渲染证据 | 静态 PASS / 实机 NOT_RUN |
| Sprite 引用有效且导入为 Sprite | 4 个 GUID、3 个 Builder、3 个 Prefab 静态契约通过 | PASS |
| 新资源在实际商店路径中可见 | 当前 Prefab 触发旧版 runtime fallback；item frame 未被加载，board art 存在被后创建不透明板覆盖的风险 | FAIL / 待截图复核 |

静态检查只证明序列化和代码路径，不能替代 Unity 渲染、输入因果、Console 或目标分辨率证据。
