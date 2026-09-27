# 《深压》：缺氧参考、现状诊断与设计 Skill 筛选

核查日期：2026-09-27。范围：先研究原版《缺氧》的设计，再筛选 GitHub skills，并纳入用户追加的电力、电线与发电机悬空截图。本轮未修改游戏代码、未安装第三方 skill、未执行第三方仓库脚本。

## 结论

当前缺少的是“玩家为什么要建、建完改变什么”的系统联系。增加科技和菜单的数量，没有使供气、用电、采矿和研究形成必要的经营选择。界面和动效必须表达这些实际状态。

建议先做一个能完整玩通的供气工程：**有限储气下降 → 选择气源 → 开挖和供电 → 输送、缓冲与处理 → 改善工作区域 → 扩展下一处气藏**。科技提供不同解决手段，资源限制决定处理顺序，设备反馈让玩家看懂变化。

## 1. 已核实的参考与证据边界

没有在本机运行《缺氧》。官方页面与图片的浏览器加载两次超时，本轮没有直接目检其当前科技树画面。以下来自可读的官方资料和设计负责人直接访谈；《深压》的具体布局与规则是设计建议，不冒充照着当前截图复刻。

| 参考事实 | 一手来源 | 对本项目的意义 |
|---|---|---|
| 生存资源持续耗竭；开采、气液、电力、温度和回收互相影响 | [本体 Steam 官方介绍](https://store.steampowered.com/app/457140/Oxygen_Not_Included/) | 设施应有持续输入、产出及后果，玩家必须取舍；不能只是在目录中购买更大的数字 |
| 空气叠加视图显示流动、CO₂ 积累和制氧过程 | [同一官方介绍的供氧部分](https://store.steampowered.com/app/457140/Oxygen_Not_Included/) | 把供气不足、流向和污染显示在世界里；详细成分按需展开 |
| 研究界面曾加入按类别/建筑筛选；发电机要等电线完工才算连接 | [Klei 官方更新 290148，2018-10-17](https://kleiforums.com/game-updates/oni-alpha/290148-r589/) | 科技按玩家想解决的工程问题检索；电线是实际施工与连通关系。此处引用历史规则，不声称复现当前全部数值 |
| 设计负责人重视通过试玩找出理解阻点，让玩家自己发现规律 | [Johann Seidenz 直接访谈，2017-06-09](https://www.gamedeveloper.com/design/-tutorials-are-not-fun-so-a-klei-dev-offers-tips-on-avoiding-them) | 从场景、因果与短提示引导；不要靠一张长任务清单解释为什么这个游戏值得玩 |
| 设备动画会随实际物料变化，靠墙/地板的动画接触问题也需要专门修正 | [Klei 全版本更新 700386，2025-11-20](https://kleiforums.com/game-updates/oni-alpha/700386-r2633/) | 动效要对应真实运行状态；人物脚、机器底脚、操作点和地面要有统一基准 |

## 2. 当前项目的问题与证据

### 素材确实悬空，不能只改根对象坐标

用户截图中，发电机可见底座与金属地面之间有明显间隙。源文件核查得到相同结论：

- `Assets/DeepPressure/Editor/DeepCatalogBuilder.cs:211` 将机器按整幅 Sprite 尺寸居中适配占格，`:248` 的 `Fit()` 使用 `sprite.bounds`，没有把可见底脚作为对齐点。
- 对现有 PNG 的 alpha > 25 可见范围、PPU、pivot 和 prefab 缩放/位移作估算，发电机底脚约高出占地底面 **0.38 格**；仓库约 **0.26 格**；研究台约 **0.27 格**。这些是当前资源的诊断估值，不是最终调整参数。
- 保存场景 `DeepPressureColony 2.unity` 中两台发电机引用该 prefab，根对象位于 y=53；因此根坐标对齐并不代表图片底脚对齐。

修正应为素材定义 `groundAnchor`（可见底脚）、占格、操作点和端口。落地设备按底脚对齐，墙挂和吊挂设备按安装点对齐，接触阴影随安装面变化。修复 prefab 生成规则，并迁移已有实例；不能仅拖低截图里的单个实例。

### 电力目前是全局数值，缺少电网

- `Runtime/Gameplay/DeepGameSession.cs:149` 的供电更新汇总所有建筑的产能和需求；任意位置的发电机都能满足另一处设备。
- `Data/Gameplay/Buildings/generator.asset:33` 发电量为 20；`:35` 的燃料为空，`:36` 消耗率为 0。当前配置没有燃料压力。
- `Editor/DeepColonyLevelBuilder.cs:87` 附近直接放置合成台和两台发电机。开局没有形成“先接通哪里”的选择。

必须新增电线层、端口与电网连通分量。设备没有接入同一电网就不能取电；蓝图线不导电，施工完成才导电。电源的生产、耗材和储能都应有实际数量变化。

### 气体工业与人员需求尚未接通

- `Runtime/GasNetworkSimulator.cs` 主要在 `GasNode` 之间搬运库存；目前没有管网向可工作房间供气的完整路径。
- `Runtime/Gameplay/DeepWorker.cs` 没有呼吸消耗或气氛风险响应。改善供气不能可靠地改变人员能力、可进入区域或基地续航。
- `Runtime/DeepPressureHUD.cs:138` 可以免费切换隔绝装备权限，绕过危险气氛门槛；没有相应制造、补给或人物执行成本。

因此“气罐里数字在流动”尚不等于经营循环。首个切片应将供气口、房间、人员作业条件连起来，并用有限缓冲储量给玩家可理解的应对时间。

### 资源来源和用途过于单一

`Editor/DeepCatalogBuilder.cs:61` 附近的基础配方为矿石→合金→电子元件→研究数据。挖掘产物也高度统一；资源常常是同一种原料换名。科技费用增加了劳动量，但尚未形成路线、材料特性或持续消耗方面的竞争。

建议第一阶段区分结构矿料、燃料/储能、净化耗材和气体储量；研究数据由取样和运行测量获得一部分。玩家要决定材料先用于供电、缓冲、净化还是探索，而不是只按固定次序点击合成。

### 科技树是固定卡片阵列，目标是文字序列

- `Runtime/DeepColonyCommands.cs:184` 开始绘制科技树；`:197` 用固定的 234/116 间距和 210×91 卡片。前置深度决定横坐标，缺少独立的关系图排版与连线路由。
- 节点主要重复文字状态，没有把解锁设施图标、工程问题与路径选择放在第一层。
- 同文件 `DrawMission()` 用研究台与几个科技的解锁情况推进阶段；它不会实际验证“已经形成稳定供气/完成双出口工程”。文字目标推进可能领先于实际工程。
- 目前未用操作锁强迫玩家完成所有步骤，但呈现方式仍像指定顺序的任务清单，不能靠世界反馈自然发现需求。

## 3. 电力与资源的最小完整设计

这是拟实施规则，不是现有功能或《缺氧》精确复刻。

| 部分 | 必须形成的行为 | 验收方法 |
|---|---|---|
| 电源 | 实际消耗燃料，或占用一名人员手动发电；输出有上限 | 断料/离岗后产能归零，设备与仪表同步变化 |
| 电线 | 从设备电端口按格规划，计材料与工时，构成独立网络 | 只画蓝图不能供电；剪断一段后只影响该支路 |
| 储能 | 记录能量容量、充放电功率与损耗规则，停止发电后逐步耗尽 | 比较充放电前后能量，不能凭空产生电或负库存 |
| 用电设备 | 先检查连接，再检查网络供需与设备优先级 | 独立的“未接线”“电源无燃料”“电网不足”图标；不只给统一报错 |
| 供气链 | 气源→泵/处理→缓冲→房间供气口→实际作业需求 | 断电后供气停止，缓冲继续支撑一段时间，恢复后重新稳定 |
| 生产控制 | 目标库存、暂停/继续、最低保留量、成品仓满停产 | 满仓不吞材料，低于阈值恢复；显示可用量与预留量 |
| 电力叠加层 | 显示连通网络、端口、当前供需和故障位置 | 正常视图不被电线遮满；切层可从故障设备追到断点 |

电线和气管使用不同端口形状、线型与层级，避免只靠颜色区分。复杂变压器、全套热损坏和多电压网络后置，先验证连通、材料消耗、缓冲和断线恢复。

开局应保留足够容错的应急储气与供电，让玩家能观察并修正；完整工业演示线留在演示/沙盒场景，避免提前解决新游戏要交给玩家的问题。具体初始数量需要试玩再定，当前不宣称已平衡。

## 4. 布局、非文字反馈与精修规范

### 科技界面

科技浏览区和稳定的详情区分开。节点第一眼呈现解锁设施图标与短名称；详细材料、前置、工时和收益集中在详情区。保留搜索、分支筛选、拖拽/缩放；选中时突出前置链，其他连线退后。已掌握、可研究、缺前置、研究中采用图形加色彩区分。

科技应回答“它能解决哪种工程困难”，例如：缓冲储气应对供气波动、选择分离处理杂质、储能承接供电间歇。不要用增加卡片数量制造复杂度。

### 非文字引导

| 玩家遇到的问题 | 世界中的第一层反馈 | 按需第二层信息 |
|---|---|---|
| 设备没电 | 动作停止，插头图标指向电端口 | 点击后显示断线位置或该网供需 |
| 储气下降 | 罐体刻度和趋势箭头下降 | 悬停查看净消耗与预计剩余时间 |
| 原料缺失 | 投料口空，缺料图标固定在设备上 | 点选查看缺的材料与运输来源 |
| 管道方向错误 | 明确的输入/输出口形状和流向预览 | 无需反复弹出通用错误提示 |
| 新科技可用 | 对应建筑图标短暂高亮一次 | 玩家自行打开研究；不抢镜头、不锁工具 |

提示可关闭且记住关闭状态。没有文字教程时仍应能发现最基本问题；保留可展开说明，不能把“非文字”误做成只能猜的神秘图标。

### 动效与素材

统一素材家族的视角、光源方向、轮廓粗细、PPU、人物相对尺寸及颜色层次。逐个检查可见底脚、操作点、端口和阴影，而不只检查 PNG 分辨率。

动效按事件组织：选择有即时反馈；施工有准备、循环、完成；机器转速跟随实际作业；断电进入停机状态；气流跟随实际方向。高频框选和快捷键不能被入场动画拖慢。少量菜单过渡用不受模拟倍速影响的时间，世界机械动作跟随模拟时间；任何表现效果都不改动工单与气体守恒结果。

## 5. GitHub Skill 筛选

筛选依据是已读到真实 `SKILL.md`、可用参考文件、内容具体程度、来源、许可与项目适配，而非仅看名称或 star 数。以下“优先”表示适合承担相应子任务，不表示已经在这个项目运行验证。

| 优先度 | 原始 Skill | 用在这里的具体工作 | 限制 |
|---|---|---|---|
| 优先 | [interface-design](https://github.com/Dammyjay93/interface-design/blob/main/.claude/skills/interface-design/SKILL.md) | 科技树/建造菜单的信息层级、密度、间距、组件状态和统一规则 | 面向产品界面，Web 实现需转为 Unity；[MIT](https://github.com/Dammyjay93/interface-design/blob/main/LICENSE) |
| 优先 | [Emil Kowalski animate](https://github.com/emilkowalski/skills/blob/main/skills/animate/SKILL.md) | 判断哪些操作值得动画，规定用途、时长、缓动、中断与退出；附 RECIPES.md | CSS/WAAPI/Motion 示例不能直接搬进 Unity；[MIT](https://github.com/emilkowalski/skills/blob/main/LICENSE) |
| 优先 | [create-game-assets](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/create-game-assets/SKILL.md) | 素材家族规范、pivot/PPU/透明边界、导入清单、拼接及游戏内实际尺寸验收；最对应悬空问题 | QA 脚本需要 Python/Pillow，仍要根据本项目资源改造；[Apache-2.0](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/LICENSE) |
| 辅助 | [Impeccable](https://github.com/pbakaus/impeccable/blob/main/.agents/skills/impeccable/SKILL.md) | 独立的 layout、polish、onboard、animate 流程，逐项审查布局/收尾/可跳过引导 | 当前是统一技能，带 launcher/二进制；本轮只读方法，浏览器检测器不能证明 Unity 界面正确；[Apache-2.0](https://github.com/pbakaus/impeccable/blob/main/LICENSE) |
| 辅助 | [unity-ui](https://github.com/Nice-Wolf-Studio/unity-claude-skills/blob/main/skills/unity-ui/SKILL.md) 与 [unity-2d](https://github.com/Nice-Wolf-Studio/unity-claude-skills/blob/main/skills/unity-2d/SKILL.md) | 原生布局、锚点、事件生命周期、文字、Sprite/Tilemap/灯光与排序 | 以 Unity 6.3 为目标，本项目是 2022.3；API 和架构选择必须按版本核对，不能无条件照搬“uGUI 仅旧项目”等概括；[MIT](https://github.com/Nice-Wolf-Studio/unity-claude-skills/blob/main/LICENSE) |
| 方法参考 | [design-game-experience](https://github.com/5681jin/game-design-skills/blob/main/design-game-experience/SKILL.md) | 从玩家体验检查核心循环、资源机会成本、首局理解和试玩验证；包含评审与交付参考文件 | 内容贴题，但成熟度证据不足，本轮未找到 LICENSE；暂不作为可直接打包分发的成熟依赖 |

Impeccable 已具体核查 [layout](https://github.com/pbakaus/impeccable/blob/main/.agents/skills/impeccable/reference/layout.md)、[onboard](https://github.com/pbakaus/impeccable/blob/main/.agents/skills/impeccable/reference/onboard.md)、[animate](https://github.com/pbakaus/impeccable/blob/main/.agents/skills/impeccable/reference/animate.md)；其引导方法要求真实使用情境、可略过、逐步披露，能帮助替换目前的文字清单。

不作为主选：

- [Anthropic frontend-design](https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md) 确实是原始官方技能，适合视觉方向，但无法补齐游戏经济与工程因果。
- [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) 的样式/配色资料可补充查阅，当前技术栈列表不包含 Unity。
- [game-feel](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/game-feel/SKILL.md) 的事件分级可用；动作游戏震屏/停帧不适合频繁经营操作，示例将 `Time.timeScale` 恢复到 1 会干扰当前暂停/倍速，不能直接抄。
- 已安装的 power-design 面向 HTML 网站与演示，不作为本次 Unity 游戏 HUD 的主工作流。

## 6. 建议实施顺序与验收

1. **先修所有物体接触关系**：落地设备的可见底脚接触地面；挂墙设备有安装点；新建、复制、读档都保持一致。以用户提供的发电机截图作为回归案例。
2. **接通一个供电—供气—需求闭环**：燃料/人力、已施工电线、设备、缓冲储气与房间需求互相作用。至少两种可行处理顺序，各有材料和时间代价。
3. **再做科技界面与资源控制**：检索问题、选择方案、显示当前缺失条件；生产量、消耗量、可用/预留量和库存阈值可以被理解及控制。
4. **最后统一非文字反馈与美术精修**：保证基础规则已经成立，动画表现真实状态；用首次接触的玩家检验能否在不读任务清单的情况下识别并修复缺电/缺气。

验证包含：断线只影响其所属网络；未施工线不导电；停止生产会消耗缓冲；没电时机器不假装运行；仓满不会吞料；研究确实增加可行方案；界面在常用分辨率和中文长文本下可用。以上目前是验收要求，未声称已经通过。
