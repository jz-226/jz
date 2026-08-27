const modal = document.getElementById('videoModal');
const modalVideo = document.getElementById('modalVideo');
const closeModal = () => { modal.classList.remove('open'); modal.setAttribute('aria-hidden', 'true'); modalVideo.pause(); modalVideo.removeAttribute('src'); };
document.querySelectorAll('[data-video]').forEach((button) => button.addEventListener('click', () => { modalVideo.src = button.dataset.video; modal.classList.add('open'); modal.setAttribute('aria-hidden', 'false'); modalVideo.play().catch(() => {}); }));
document.querySelectorAll('[data-wind]').forEach((button) => button.addEventListener('click', () => document.querySelector('.feature-orange').scrollIntoView({ behavior: 'smooth', block: 'center' })));
document.getElementById('modalClose').addEventListener('click', closeModal);
modal.addEventListener('click', (event) => { if (event.target === modal) closeModal(); });
document.addEventListener('keydown', (event) => { if (event.key === 'Escape') closeModal(); });

const caseData = {
  offerpilot: { count: '01', type: 'AI PRODUCT', kicker: 'CASE STUDY / OFFERPILOT', title: '从目标岗位，到每天的一步。', lead: '一个把职业成长拆成可执行反馈回路的 AI 产品。重点不是“聊天”，而是让用户持续知道下一步应该做什么。', meta: [['我的职责', '产品定义 · UX · 前端 · AI 流程'], ['关键产出', '150+ 岗位 · 17 组 JD 匹配'], ['交付状态', 'Vercel 已上线']], sections: [['01 / PROBLEM', '求职信息很多，但用户不知道差距在哪里，也不知道今天应该先补什么。'], ['02 / SYSTEM', '目标设定 → 人岗差距分析 → 成长路线 → 每日测验 → readiness 更新。'], ['03 / INTERACTION', '用路线、阶段和每日任务把抽象的“成长”变成可以被完成、被记录的动作。'], ['04 / BUILD', 'Next.js 16、Supabase、DeepSeek。独立完成产品结构、界面和上线。']], links: [['打开线上产品', 'https://offerpilot-kappa.vercel.app/'], ['查看 GitHub', 'https://github.com/jz-226/offerpilot'], ['观看产品录屏', 'video:assets/offerpilot-showcase.mp4']] },
  wind: { count: '02', type: 'AI OPERATIONS', kicker: 'CASE STUDY / MARKETING WIND TUNNEL', title: '先被用户攻击，再决定上线。', lead: '一个给运营人员用的 AI 营销方案碰撞实验室。它把“凭感觉上线”改成一轮可观察、可回应、可复测的决策过程。', meta: [['我的职责', '创意 · 产品 · 交互 · 前端 · Prompt'], ['关键机制', '10 个消费者人格 · 4 个 AI 接口'], ['交付状态', 'AI 开发者大赛 / AI+运营']], sections: [['01 / INPUT', '运营者输入一份营销方案，系统将它作为待测试的假设。'], ['02 / COLLISION', '十个不同消费者人格提出质疑，用户可以拖拽其中一条进入修复舱。'], ['03 / EVOLUTION', 'AI 根据回应给出评分与修改点，生成优化方案，再跑第二轮对比。'], ['04 / DECISION', '决策台汇总风险等级、最具攻击性的用户和“能不能上线”的建议。']], links: [['打开线上 Demo', 'https://www.modelscope.cn/studios/jiangzi226/Marketing-Wind-Tunnel']] },
  tutor: { count: '03', type: 'SHIPPED MINI PROGRAM', kicker: 'CASE STUDY / JIANG JI XUEQING BEN', title: '把每节课留下来。', lead: '一个给家教老师使用的轻量教学工作台。围绕错题状态流转，帮助老师快速查看学生情况、安排课程并沉淀课后信息。', meta: [['我的职责', '需求梳理 · 交互 · 视觉 · 原生开发'], ['核心模块', '学生档案 · 备课 · 错题 · 总结'], ['交付状态', '微信小程序已上线']], sections: [['01 / CONTEXT', '家教工作往往分散在聊天记录、纸张和临时笔记里，课后信息很容易丢失。'], ['02 / STRUCTURE', '首页统计今日课程、待讲错题与周进度，底部导航固定四个最高频入口。'], ['03 / STATE', '错题从“待讲解”到“已讲解”再到“已掌握”，让学习状态有明确的下一步。'], ['04 / COURSE FLOW', '创建课程时填写学生、科目、日期、状态、教学目标、知识点和练习题，让备课信息在课前就结构化。'], ['05 / BUILD', '原生 WXML / WXSS / JS，自定义 tabBar，使用 wx.setStorageSync 实现开箱即用。']], links: [['扫码体验小程序', '#work'], ['观看创建课程录屏', 'video:assets/tutor-create-course.mp4']] }
,
  before8: { count: '04', type: 'GAME DESIGN & DEVELOPMENT', kicker: 'CASE STUDY / BEFORE 8AM', title: '在早八以前，离开这里。', lead: '《早八在逃》把熟悉的大学生活区翻转成午夜异常空间：玩家需要在有限时间内探索、搜刮、避开巡夜者，并决定何时带着战利品撤离。', meta: [['我的职责', '世界观 · 核心循环 · 系统设计 · Unity 开发'], ['核心机制', '潜行 · 搜刮 · 时间碎片 · 撤离抉择'], ['交付状态', '开发中 / 垂直切片']], sections: [['01 / PREMISE', '凌晨 0 点以后，校园不再属于人类。玩家从宿舍翻窗进入，目标是在早八以前找到时间碎片，开启晨门并逃出去。'], ['02 / RUN LOOP', '翻窗 → 探索与搜刮 → 收集 3 个时间碎片 → 开启晨门 → 立即撤离或继续冒险。每一局都需要在安全与收益之间做选择。'], ['03 / STEALTH', '巡夜者通过距离、视角和遮挡判断玩家位置，发现后的追逐仍留有脱身空间；“绕墙躲开视线”是核心操作。'], ['04 / BUILD', '基于 Unity 2022.3 LTS 制作，使用 URP、Input System、AI Navigation 与 ScriptableObject 数据驱动，面向 PC 开发并考虑后续移动端体验。']] }
};
const drawer = document.getElementById('caseDrawer');
const drawerClose = document.getElementById('drawerClose');
const drawerTitle = document.getElementById('drawerTitle');
const drawerKicker = document.getElementById('drawerKicker');
const drawerLead = document.getElementById('drawerLead');
const drawerMeta = document.getElementById('drawerMeta');
const drawerSections = document.getElementById('drawerSections');
const drawerLinks = document.getElementById('drawerLinks');
document.getElementById('drawerCount').textContent = '01';
function openCase(key) {
  const data = caseData[key]; if (!data) return;
  document.getElementById('drawerCount').textContent = data.count; document.getElementById('drawerType').textContent = data.type;
  drawerKicker.textContent = data.kicker; drawerTitle.textContent = data.title; drawerLead.textContent = data.lead;
  drawerMeta.innerHTML = data.meta.map(([label, value]) => `<span><b>${label}</b>${value}</span>`).join('');
  drawerSections.innerHTML = data.sections.map(([title, copy]) => `<div class="drawer-section"><h3>${title}</h3><p>${copy}</p></div>`).join('');
  drawerLinks.innerHTML = data.links.map(([label, href]) => href.startsWith('video:') ? `<button class="drawer-video" data-video="${href.slice(6)}">${label} ↗</button>` : `<a href="${href}" ${href.startsWith('http') ? 'target="_blank" rel="noreferrer"' : ''}>${label} ↗</a>`).join('');
  drawer.classList.add('is-open'); drawer.setAttribute('aria-hidden', 'false'); document.body.style.overflow = 'hidden';
  drawer.querySelectorAll('.drawer-video').forEach((button) => button.addEventListener('click', () => { closeDrawer(); modalVideo.src = button.dataset.video; modal.classList.add('open'); modalVideo.play().catch(() => {}); }));
}
function closeDrawer() { drawer.classList.remove('is-open'); drawer.setAttribute('aria-hidden', 'true'); document.body.style.overflow = ''; }
document.querySelectorAll('.case-trigger').forEach((button) => button.addEventListener('click', (event) => { event.stopPropagation(); openCase(button.dataset.case); }));
document.querySelectorAll('.feature[data-case]').forEach((feature) => feature.addEventListener('click', (event) => { if (!event.target.closest('a,button')) openCase(feature.dataset.case); }));
drawerClose.addEventListener('click', closeDrawer); document.addEventListener('keydown', (event) => { if (event.key === 'Escape') closeDrawer(); });

const cursor = document.getElementById('cursor');
window.addEventListener('pointermove', (event) => { cursor.style.left = `${event.clientX}px`; cursor.style.top = `${event.clientY}px`; });
document.querySelectorAll('a,button,.feature,.visual figure').forEach((el) => { el.addEventListener('mouseenter', () => cursor.classList.add('is-hover')); el.addEventListener('mouseleave', () => cursor.classList.remove('is-hover')); });
const revealObserver = new IntersectionObserver((entries) => entries.forEach((entry) => { if (entry.isIntersecting) revealObserver.unobserve(entry.target), entry.target.classList.add('is-visible'); }), { threshold: .14 });
document.querySelectorAll('.reveal').forEach((el) => revealObserver.observe(el));
const loopState = { observe: ['观察真实问题', '从用户处境开始，而不是从一个漂亮界面开始。', '#b8dd54'], shape: ['形成可用方案', '把洞察组织成流程、界面和可以被验证的选择。', '#ff7543'], ship: ['交付并验证', '让产品真正上线，被使用，再根据反馈继续生长。', '#35e7ee'] };
const visualCore = document.querySelector('.intro-visual'); const loopTitle = document.getElementById('loopTitle'); const loopCopy = document.getElementById('loopCopy');
function selectLoop(key) { const state = loopState[key]; if (!state) return; visualCore.dataset.loopState = key; loopTitle.textContent = state[0]; loopCopy.textContent = state[1]; loopTitle.style.color = state[2]; document.querySelectorAll('.loop-label').forEach((label) => label.classList.toggle('is-active', label.dataset.loop === key)); window.setSphereMode?.(key); }
document.querySelectorAll('[data-loop]').forEach((button) => button.addEventListener('click', () => selectLoop(button.dataset.loop))); selectLoop('observe');

function initSphere() {
  const host = document.getElementById('sphere');
  if (!host || !window.THREE) return;
  const THREE = window.THREE;
  const canvas = document.createElement('canvas'); host.appendChild(canvas);
  const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(35, 1, .1, 100); camera.position.z = 5.8;
  const group = new THREE.Group(); scene.add(group);
  const coreMaterial = new THREE.MeshPhysicalMaterial({ color: 0x306cf3, roughness: .22, metalness: .2, clearcoat: .7, emissive: 0x0a1f65, emissiveIntensity: .5 });
  const mesh = new THREE.Mesh(new THREE.IcosahedronGeometry(1.23, 3), coreMaterial); group.add(mesh);
  group.add(new THREE.LineSegments(new THREE.WireframeGeometry(new THREE.IcosahedronGeometry(1.3, 2)), new THREE.LineBasicMaterial({ color: 0xb8dd54, transparent: true, opacity: .65 })));
  const ring = new THREE.Mesh(new THREE.TorusGeometry(1.82, .012, 10, 100), new THREE.MeshBasicMaterial({ color: 0xff7543, transparent: true, opacity: .65 })); ring.rotation.x = .8; group.add(ring);
  const nodeColors = [0xb8dd54, 0xff7543, 0x35e7ee];
  const nodes = nodeColors.map((color, index) => { const node = new THREE.Mesh(new THREE.SphereGeometry(.12, 12, 12), new THREE.MeshBasicMaterial({ color })); node.userData.phase = index * (Math.PI * 2 / 3); group.add(node); return node; });
  const modes = { observe: [0x6aa330, 0xb8dd54], shape: [0xff7543, 0xff7543], ship: [0x35c9df, 0x35e7ee] };
  window.setSphereMode = (mode) => { const colors = modes[mode]; if (!colors) return; coreMaterial.color.setHex(colors[0]); coreMaterial.emissive.setHex(colors[0]); ring.material.color.setHex(colors[1]); };
  window.setSphereMode('observe');
  scene.add(new THREE.AmbientLight(0xffffff, 1.8)); const light = new THREE.PointLight(0x7eb5ff, 8, 10); light.position.set(3, 2, 4); scene.add(light);
  const resize = () => { const rect = host.getBoundingClientRect(); renderer.setSize(rect.width, rect.height, false); camera.aspect = rect.width / rect.height; camera.updateProjectionMatrix(); }; resize(); window.addEventListener('resize', resize);
  let mx = 0; let my = 0; host.addEventListener('pointermove', (event) => { const r = host.getBoundingClientRect(); mx = ((event.clientX - r.left) / r.width - .5) * .45; my = ((event.clientY - r.top) / r.height - .5) * .35; });
  const tick = () => { const t = performance.now() * .0004; group.rotation.y += .003; group.rotation.x += (my - group.rotation.x) * .03; group.position.x += (mx - group.position.x) * .03; ring.rotation.z -= .006; nodes.forEach((node) => { const a = t + node.userData.phase; node.position.set(Math.cos(a) * 1.72, Math.sin(a * 1.25) * .82, Math.sin(a) * 1.2); }); renderer.render(scene, camera); requestAnimationFrame(tick); }; tick();
}
initSphere();
