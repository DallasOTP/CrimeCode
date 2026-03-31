// === CrimeCode Forum — Frontend Application (craxpro.to style) ===

const API = '/api';
let currentUser = null;
let currentPage = 1;
let currentCategory = null;
let badgeInterval = null;
let heartbeatInterval = null;
let shoutboxInterval = null;
let cachedTags = null;

// === Init ===
document.addEventListener('DOMContentLoaded', () => {
    // Telegram Mini App detection & theme
    initTelegram();

    const saved = localStorage.getItem('crimecode_user');
    if (saved) {
        try {
            currentUser = JSON.parse(saved);
            refreshMe();
        } catch { localStorage.removeItem('crimecode_user'); }
    }
    // Auto-login via Telegram if inside Mini App
    if (!currentUser && window.Telegram?.WebApp?.initData) {
        telegramAutoLogin();
    }
    updateAuthUI();
    loadShoutbox();
    loadForumStats();
    loadHome();
});

// === Telegram Mini App ===
function initTelegram() {
    const tg = window.Telegram?.WebApp;
    if (!tg) return;
    tg.ready();
    tg.expand();
    // Apply Telegram theme colors to CSS variables
    applyTelegramTheme(tg.themeParams);
    tg.onEvent('themeChanged', () => applyTelegramTheme(tg.themeParams));
    document.body.classList.add('tg-mini-app');
}

function applyTelegramTheme(tp) {
    if (!tp) return;
    const root = document.documentElement.style;
    if (tp.bg_color) root.setProperty('--bg-primary', tp.bg_color);
    if (tp.secondary_bg_color) root.setProperty('--bg-secondary', tp.secondary_bg_color);
    if (tp.text_color) root.setProperty('--text-primary', tp.text_color);
    if (tp.hint_color) root.setProperty('--text-secondary', tp.hint_color);
    if (tp.link_color) root.setProperty('--accent-cyan', tp.link_color);
    if (tp.button_color) root.setProperty('--accent', tp.button_color);
    if (tp.button_text_color) root.setProperty('--btn-text', tp.button_text_color);
    if (tp.section_bg_color) root.setProperty('--bg-card', tp.section_bg_color);
}

async function telegramAutoLogin() {
    try {
        const data = await api('/auth/telegram', {
            method: 'POST',
            body: JSON.stringify({ initData: window.Telegram.WebApp.initData })
        });
        if (data?.token) {
            currentUser = { token: data.token, userId: data.userId, username: data.username };
            localStorage.setItem('crimecode_user', JSON.stringify(currentUser));
            refreshMe();
        }
    } catch (e) {
        console.warn('Telegram auto-login failed:', e);
    }
}

// === API Helper ===
async function api(path, options = {}) {
    const headers = { 'Content-Type': 'application/json', ...options.headers };
    if (currentUser?.token) headers['Authorization'] = `Bearer ${currentUser.token}`;
    const res = await fetch(`${API}${path}`, { ...options, headers });
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, data };
    return data;
}

async function apiRaw(path, options = {}) {
    const headers = { ...options.headers };
    if (currentUser?.token) headers['Authorization'] = `Bearer ${currentUser.token}`;
    const res = await fetch(`${API}${path}`, { ...options, headers });
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, data };
    return data;
}

// === Refresh /me ===
async function refreshMe() {
    try {
        const me = await api('/auth/me');
        currentUser = { ...currentUser, ...me };
        localStorage.setItem('crimecode_user', JSON.stringify(currentUser));
        updateAuthUI();
        startBadgePolling();
        startHeartbeat();
    } catch {
        logout();
    }
}

// === Navigation ===
function navigate(page, params = {}) {
    document.querySelectorAll('#mainContent > div').forEach(d => d.style.display = 'none');
    // Update nav links
    document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));
    const navLink = document.querySelector(`.nav-link[data-page="${page}"]`);
    if (navLink) navLink.classList.add('active');

    switch (page) {
        case 'home':
            document.getElementById('pageHome').style.display = '';
            loadHome();
            break;
        case 'thread':
            document.getElementById('pageThread').style.display = '';
            loadThread(params.id);
            break;
        case 'search':
            document.getElementById('pageSearch').style.display = '';
            break;
        case 'profile':
            document.getElementById('pageProfile').style.display = '';
            loadProfile(params.id);
            break;
        case 'notifications':
            document.getElementById('pageNotifications').style.display = '';
            loadNotifications();
            break;
        case 'messages':
            document.getElementById('pageMessages').style.display = '';
            if (params.userId) loadConversation(params.userId);
            else loadConversations();
            break;
        case 'marketplace':
            document.getElementById('pageMarketplace').style.display = '';
            loadMarketplace();
            break;
        case 'leaderboard':
            document.getElementById('pageLeaderboard').style.display = '';
            loadLeaderboard('reputation');
            break;
        case 'members':
            document.getElementById('pageMembers').style.display = '';
            loadOnlineUsers();
            break;
        case 'admin':
            document.getElementById('pageAdmin').style.display = '';
            loadAdmin();
            break;
    }
    closeUserMenu();
    window.scrollTo(0, 0);
}

// === Auth ===
function updateAuthUI() {
    const nav = document.getElementById('headerNav');
    const navLogged = document.getElementById('headerNavLogged');
    if (currentUser) {
        nav.style.display = 'none';
        navLogged.style.display = 'flex';
        document.getElementById('userGreeting').textContent = currentUser.username || currentUser.Username || 'User';
        // Credits
        const cc = document.getElementById('creditCount');
        if (cc) cc.textContent = currentUser.credits ?? 0;
        // Avatar
        const avatarEl = document.getElementById('userMenuAvatar');
        if (avatarEl) {
            if (currentUser.avatarUrl) {
                avatarEl.innerHTML = `<img src="${escapeHtml(currentUser.avatarUrl)}" alt="">`;
            } else {
                avatarEl.textContent = (currentUser.username || '?').charAt(0).toUpperCase();
            }
        }
        // Role / Admin link
        try {
            const payload = JSON.parse(atob(currentUser.token.split('.')[1]));
            const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
            currentUser.role = role;
            const adminLink = document.getElementById('adminLink');
            if (adminLink) adminLink.style.display = (role === 'Admin' || role === 'Moderator') ? '' : 'none';
        } catch { /* ignore */ }
        // Show shoutbox input  
        const sbInput = document.getElementById('shoutboxInput');
        if (sbInput) sbInput.style.display = 'flex';
        // Show new listing btn
        const nlb = document.getElementById('newListingBtn');
        if (nlb) nlb.style.display = '';
        updateChatVisibility();
    } else {
        nav.style.display = 'flex';
        navLogged.style.display = 'none';
        const sbInput = document.getElementById('shoutboxInput');
        if (sbInput) sbInput.style.display = 'none';
        const nlb = document.getElementById('newListingBtn');
        if (nlb) nlb.style.display = 'none';
        updateChatVisibility();
    }
}

async function register(e) {
    e.preventDefault();
    const errEl = document.getElementById('registerError');
    errEl.textContent = '';
    try {
        const data = await api('/auth/register', {
            method: 'POST',
            body: JSON.stringify({
                username: document.getElementById('regUsername').value.trim(),
                email: document.getElementById('regEmail').value.trim(),
                password: document.getElementById('regPassword').value
            })
        });
        currentUser = data;
        localStorage.setItem('crimecode_user', JSON.stringify(data));
        refreshMe();
        closeModal();
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore durante la registrazione';
    }
}

async function login(e) {
    e.preventDefault();
    const errEl = document.getElementById('loginError');
    errEl.textContent = '';
    try {
        const data = await api('/auth/login', {
            method: 'POST',
            body: JSON.stringify({
                email: document.getElementById('loginEmail').value.trim(),
                password: document.getElementById('loginPassword').value
            })
        });
        currentUser = data;
        localStorage.setItem('crimecode_user', JSON.stringify(data));
        refreshMe();
        closeModal();
        loadHome();
    } catch {
        errEl.textContent = 'Email o password non validi';
    }
}

function logout() {
    currentUser = null;
    localStorage.removeItem('crimecode_user');
    updateAuthUI();
    stopBadgePolling();
    stopHeartbeat();
    navigate('home');
}

// === User Menu ===
function toggleUserMenu() {
    const dd = document.getElementById('userDropdown');
    dd.classList.toggle('show');
}
function closeUserMenu() {
    const dd = document.getElementById('userDropdown');
    if (dd) dd.classList.remove('show');
}
document.addEventListener('click', (e) => {
    if (!e.target.closest('.user-menu')) closeUserMenu();
});

// === Badge Polling ===
function startBadgePolling() {
    updateBadges();
    if (badgeInterval) clearInterval(badgeInterval);
    badgeInterval = setInterval(updateBadges, 30000);
}
function stopBadgePolling() {
    if (badgeInterval) { clearInterval(badgeInterval); badgeInterval = null; }
    setBadge('notifBadge', 0);
    setBadge('msgBadge', 0);
    setBadge('chatBadge', 0);
}
async function updateBadges() {
    if (!currentUser) return;
    try {
        const [n, m, c] = await Promise.all([
            api('/notifications/unread-count'),
            api('/messages/unread-count'),
            api('/chat/unread-count')
        ]);
        setBadge('notifBadge', n.count);
        setBadge('msgBadge', m.count);
        setBadge('chatBadge', c.count);
    } catch { /* ignore */ }
}
function setBadge(id, count) {
    const el = document.getElementById(id);
    if (!el) return;
    if (count > 0) {
        el.textContent = count > 99 ? '99+' : count;
        el.style.display = '';
    } else {
        el.style.display = 'none';
    }
}

// === Heartbeat ===
function startHeartbeat() {
    if (heartbeatInterval) clearInterval(heartbeatInterval);
    heartbeatInterval = setInterval(async () => {
        try { await api('/users/heartbeat', { method: 'POST' }); } catch { /* ignore */ }
    }, 60000);
}
function stopHeartbeat() {
    if (heartbeatInterval) { clearInterval(heartbeatInterval); heartbeatInterval = null; }
}

// === Forum Stats ===
async function loadForumStats() {
    try {
        const s = await api('/leaderboard/stats');
        const bar = document.getElementById('forumStatsBar');
        if (bar) bar.textContent = `${s.totalUsers} utenti · ${s.totalThreads} thread · ${s.totalPosts} post`;
        const oc = document.getElementById('onlineCount');
        if (oc) oc.textContent = `${s.onlineUsers} online`;
        const hs = document.getElementById('heroStats');
        if (hs) hs.innerHTML = `
            <div class="hero-stat"><span class="hero-stat-value">${s.totalUsers}</span><span class="hero-stat-label">Utenti</span></div>
            <div class="hero-stat"><span class="hero-stat-value">${s.totalThreads}</span><span class="hero-stat-label">Thread</span></div>
            <div class="hero-stat"><span class="hero-stat-value">${s.totalPosts}</span><span class="hero-stat-label">Post</span></div>
            <div class="hero-stat"><span class="hero-stat-value">${s.onlineUsers}</span><span class="hero-stat-label">Online</span></div>`;
        const fs = document.getElementById('footerStats');
        if (fs) fs.textContent = `${s.totalUsers} utenti · ${s.totalThreads} thread · ${s.totalPosts} post · ${s.onlineUsers} online`;
    } catch { /* ignore */ }
}

// === Modal ===
function showModal(type, params = {}) {
    const overlay = document.getElementById('modalOverlay');
    overlay.querySelectorAll('[id^="modal"]').forEach(d => {
        if (d.id !== 'modalOverlay') d.style.display = 'none';
    });
    const map = {
        login: 'modalLogin', register: 'modalRegister', newThread: 'modalNewThread',
        sendMessage: 'modalSendMessage', avatar: 'modalAvatar',
        newListing: 'modalNewListing', reputation: 'modalReputation'
    };
    const el = document.getElementById(map[type]);
    if (el) el.style.display = '';
    overlay.classList.add('active');

    if (type === 'newThread') { loadCategoriesSelect('threadCategory', false); loadTagsSelect(); }
    if (type === 'newListing') loadCategoriesSelect('listingCategory', true);
    if (type === 'sendMessage' && params.userId && params.username) {
        document.getElementById('msgReceiverId').value = params.userId;
        document.getElementById('msgReceiverName').textContent = `A: ${params.username}`;
    }
    if (type === 'reputation' && params.userId && params.username) {
        document.getElementById('repUserId').value = params.userId;
        document.getElementById('repUserName').textContent = params.username;
        document.getElementById('repPoints').value = 1;
        setRepPoints(1);
    }
    if (type === 'avatar') loadAvatarPreview();
}
function closeModal() {
    document.getElementById('modalOverlay').classList.remove('active');
}

// === Load Categories Select (for modals) ===
async function loadCategoriesSelect(selectId, marketplaceOnly) {
    const sel = document.getElementById(selectId);
    try {
        const cats = await api('/categories');
        let options = '<option value="">Seleziona categoria</option>';
        const addOptions = (list, indent = 0) => {
            for (const c of list) {
                if (marketplaceOnly && !c.isMarketplace) {
                    if (c.subCategories?.length) addOptions(c.subCategories, indent + 1);
                    continue;
                }
                const prefix = '—'.repeat(indent) + (indent ? ' ' : '');
                options += `<option value="${c.id}">${prefix}${c.icon} ${escapeHtml(c.name)}</option>`;
                if (c.subCategories?.length) addOptions(c.subCategories, indent + 1);
            }
        };
        addOptions(cats);
        sel.innerHTML = options;
    } catch { /* ignore */ }
}

// === Load Tags Select ===
async function loadTagsSelect() {
    const sel = document.getElementById('threadTag');
    try {
        if (!cachedTags) cachedTags = await api('/leaderboard/tags');
        sel.innerHTML = '<option value="">— Tag (opzionale) —</option>' +
            cachedTags.map(t => `<option value="${t.id}" style="color:${t.color}">${t.name}</option>`).join('');
    } catch { /* ignore */ }
}

// === Shoutbox ===
async function loadShoutbox() {
    try {
        const msgs = await api('/shoutbox');
        const container = document.getElementById('shoutboxMessages');
        if (msgs.length === 0) {
            container.innerHTML = '<div style="padding:0.5rem;color:var(--text-muted);font-size:12px">Nessun messaggio nella shoutbox</div>';
        } else {
            container.innerHTML = msgs.map(m => {
                const del = currentUser && (currentUser.role === 'Admin' || currentUser.role === 'Moderator')
                    ? `<span class="sb-delete" onclick="deleteShoutbox(${m.id})">✕</span>` : '';
                return `<div class="shoutbox-msg">
                    <span class="sb-author" onclick="navigate('profile',{id:${m.authorId}})">${escapeHtml(m.authorName)}</span>: 
                    ${escapeHtml(m.content)} 
                    <span class="sb-time">${timeAgo(m.createdAt)}</span>${del}
                </div>`;
            }).join('');
            container.scrollTop = container.scrollHeight;
        }
    } catch { /* ignore */ }
    // Poll shoutbox
    if (!shoutboxInterval) shoutboxInterval = setInterval(loadShoutbox, 15000);
}

function toggleShoutbox() {
    const body = document.getElementById('shoutboxBody');
    body.classList.toggle('collapsed');
    const btn = body.closest('.shoutbox').querySelector('.shoutbox-toggle');
    btn.textContent = body.classList.contains('collapsed') ? '+' : '−';
}

async function sendShoutbox() {
    const input = document.getElementById('shoutboxText');
    const content = input.value.trim();
    if (!content) return;
    try {
        await api('/shoutbox', { method: 'POST', body: JSON.stringify({ content }) });
        input.value = '';
        loadShoutbox();
    } catch (err) {
        alert(err.data?.error || 'Errore');
    }
}

async function deleteShoutbox(id) {
    try {
        await api(`/shoutbox/${id}`, { method: 'DELETE' });
        loadShoutbox();
    } catch { /* ignore */ }
}

// === Home ===
async function loadHome() {
    loadCategories();
    loadThreads();
    loadForumStats();
}

// === Categories (Hierarchical) ===
async function loadCategories() {
    const grid = document.getElementById('categoriesGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const cats = await api('/categories');
        // Filter only top-level categories (no parentId)
        const topLevel = cats.filter(c => !c.parentId);
        if (topLevel.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">📂</div><p>Nessuna categoria</p></div>';
            return;
        }
        grid.innerHTML = topLevel.map(c => {
            const subs = c.subCategories || [];
            return `<div class="forum-category">
                <div class="forum-category-header" onclick="filterByCategory(${c.id})">
                    <span class="cat-icon">${escapeHtml(c.icon)}</span>
                    <span class="cat-name">${escapeHtml(c.name)}</span>
                    <span class="cat-desc">${escapeHtml(c.description)}</span>
                </div>
                ${subs.length ? `<div class="forum-subcategories">
                    ${subs.map(s => `<div class="forum-subcategory" onclick="filterByCategory(${s.id})">
                        <span class="sub-name">${escapeHtml(s.icon)} ${escapeHtml(s.name)}</span>
                        <span class="sub-stats">${s.threadCount} thread</span>
                    </div>`).join('')}
                </div>` : ''}
            </div>`;
        }).join('');
        // Build category filter buttons
        const filtersContainer = document.getElementById('categoryFilters');
        if (filtersContainer) {
            filtersContainer.innerHTML = cats.map(c => 
                `<button class="filter-btn" onclick="currentCategory=${c.id};currentPage=1;loadThreads();setActiveFilter(this)">${escapeHtml(c.icon)} ${escapeHtml(c.name)}</button>`
            ).join('');
        }
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore nel caricamento</p></div>';
    }
}

function setActiveFilter(btn) {
    document.querySelectorAll('.section-filters .filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

function filterByCategory(id) {
    currentCategory = id;
    currentPage = 1;
    loadThreads();
}

// === Threads ===
async function loadThreads() {
    const list = document.getElementById('threadsList');
    list.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const params = new URLSearchParams({ page: currentPage, pageSize: 15 });
        if (currentCategory) params.set('categoryId', currentCategory);
        const result = await api(`/threads?${params}`);

        if (result.threads.length === 0) {
            list.innerHTML = '<div class="empty-state"><div class="empty-icon">📭</div><p>Nessun thread trovato</p></div>';
        } else {
            list.innerHTML = result.threads.map(t => threadCard(t)).join('');
        }
        renderPagination(result.total, result.page, result.pageSize);
    } catch {
        list.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore nel caricamento</p></div>';
    }
}

function threadCard(t) {
    const pinIcon = t.isPinned ? '📌 ' : '';
    const lockIcon = t.isLocked ? '🔒 ' : '';
    const avatarHtml = t.authorAvatarUrl 
        ? `<img src="${escapeHtml(t.authorAvatarUrl)}" alt="">` 
        : (t.authorName || '?').charAt(0).toUpperCase();
    const tagHtml = t.tagName 
        ? `<span class="thread-tag" style="background:${t.tagColor || '#333'};color:#fff">${escapeHtml(t.tagName)}</span>` : '';
    const prefixHtml = t.prefix ? `<span class="thread-prefix">${escapeHtml(t.prefix)}</span>` : '';
    const lastPost = t.lastPostAuthor 
        ? `<span class="last-post">Ultimo: ${escapeHtml(t.lastPostAuthor)} · ${timeAgo(t.lastPostAt)}</span>` : '';

    return `<div class="thread-card" onclick="navigate('thread',{id:${t.id}})">
        <div class="thread-card-avatar">${avatarHtml}</div>
        <div class="thread-card-body">
            <div class="thread-card-top">
                ${tagHtml}${prefixHtml}
            </div>
            <div class="thread-card-title">${pinIcon}${lockIcon}${escapeHtml(t.title)}</div>
            <div class="thread-card-meta">
                <span class="author">${escapeHtml(t.authorName)}</span>
                <span class="category-badge">${escapeHtml(t.categoryName)}</span>
                <span>${timeAgo(t.createdAt)}</span>
            </div>
        </div>
        <div class="thread-card-stats">
            <span class="stat-row">💬 ${t.postCount} · 👁 ${t.viewCount}</span>
            ${lastPost}
        </div>
    </div>`;
}

function renderPagination(total, page, pageSize) {
    const pag = document.getElementById('pagination');
    const totalPages = Math.ceil(total / pageSize);
    if (totalPages <= 1) { pag.innerHTML = ''; return; }
    let html = '';
    if (page > 1) html += `<button onclick="goToPage(${page-1})">←</button>`;
    for (let i = 1; i <= totalPages; i++) {
        if (totalPages > 7 && Math.abs(i - page) > 2 && i !== 1 && i !== totalPages) {
            if (i === 2 || i === totalPages - 1) html += `<button disabled>...</button>`;
            continue;
        }
        html += `<button class="${i === page ? 'active' : ''}" onclick="goToPage(${i})">${i}</button>`;
    }
    if (page < totalPages) html += `<button onclick="goToPage(${page+1})">→</button>`;
    pag.innerHTML = html;
}
function goToPage(page) { currentPage = page; loadThreads(); }

// === Thread Detail ===
async function loadThread(id) {
    const container = document.getElementById('threadDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const t = await api(`/threads/${id}`);
        const tagHtml = t.tagName ? `<span class="thread-tag" style="background:${t.tagColor||'#333'};color:#fff">${escapeHtml(t.tagName)}</span> ` : '';
        const prefixHtml = t.prefix ? `<span class="thread-prefix">${escapeHtml(t.prefix)}</span> ` : '';

        let html = `
            <span class="thread-back" style="display:inline-block;margin-bottom:0.8rem;cursor:pointer;color:var(--accent);font-size:13px" onclick="navigate('home')">← Torna al forum</span>
            <div class="thread-header">
                <h1>${tagHtml}${prefixHtml}${escapeHtml(t.title)}</h1>
                <div class="thread-header-meta">
                    <span>di <a href="#" onclick="event.preventDefault();navigate('profile',{id:${t.authorId}})">${escapeHtml(t.authorName)}</a></span>
                    <span>in ${escapeHtml(t.categoryName)}</span>
                    <span>${timeAgo(t.createdAt)}</span>
                    <span>👁 ${t.viewCount} visualizzazioni</span>
                    ${t.isPinned ? '<span>📌 Pinnato</span>' : ''}
                    ${t.isLocked ? '<span>🔒 Chiuso</span>' : ''}
                </div>
            </div>`;

        html += t.posts.map(p => renderPost(p, id)).join('');

        if (!t.isLocked && currentUser) {
            html += `<div class="reply-box">
                <h3>💬 Rispondi</h3>
                <textarea id="replyContent" placeholder="Scrivi la tua risposta..."></textarea>
                <button class="btn btn-primary" onclick="submitReply(${id})">Invia risposta</button>
            </div>`;
        } else if (t.isLocked) {
            html += '<div class="empty-state"><p>🔒 Questo thread è chiuso</p></div>';
        } else {
            html += '<div class="empty-state"><p><a href="#" onclick="showModal(\'login\')">Accedi</a> per rispondere</p></div>';
        }

        container.innerHTML = html;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Thread non trovato</p></div>';
    }
}

function renderPost(p, threadId) {
    const avatarHtml = p.authorAvatarUrl
        ? `<img src="${escapeHtml(p.authorAvatarUrl)}" alt="">`
        : p.authorName.charAt(0).toUpperCase();
    const likeClass = p.likedByCurrentUser ? ' liked' : '';
    const edited = p.editedAt ? ' (modificato)' : '';
    const canEdit = currentUser && currentUser.userId === p.authorId;
    const roleClass = p.authorRole === 'Admin' ? 'admin' : (p.authorRole === 'Moderator' ? 'moderator' : '');

    let authorHtml = `<div class="post-author">
        <div class="post-author-avatar">${avatarHtml}</div>
        <span class="post-author-name" onclick="navigate('profile',{id:${p.authorId}})">${escapeHtml(p.authorName)}</span>`;
    
    if (p.rankName) {
        authorHtml += `<span class="post-author-rank" style="background:${p.rankColor || '#333'};color:#fff">${p.rankIcon || ''} ${escapeHtml(p.rankName)}</span>`;
    }
    if (roleClass) {
        authorHtml += `<span class="post-author-role ${roleClass}">${escapeHtml(p.authorRole)}</span>`;
    }
    authorHtml += `<div class="post-author-stats">
        <span>📝 ${p.authorPostCount} post</span>
        <span>⭐ ${p.authorReputation} rep</span>
        <span>📅 ${new Date(p.authorJoinDate).toLocaleDateString('it-IT')}</span>
    </div>`;
    if (p.authorSignature) {
        authorHtml += `<div class="post-author-signature">${escapeHtml(p.authorSignature)}</div>`;
    }
    authorHtml += '</div>';

    let actionsHtml = `<button class="${likeClass}" onclick="event.stopPropagation();toggleLike(${threadId},${p.id},this)">❤️ ${p.likeCount}</button>`;
    if (currentUser) {
        actionsHtml += `<button onclick="event.stopPropagation();quotePost(${p.id},'${escapeHtml(p.authorName).replace(/'/g, "\\'")}',\`${escapeHtml(p.content).replace(/`/g, '\\`').replace(/\\/g, '\\\\')}\`)" title="Cita">💬 Cita</button>`;
        actionsHtml += `<button onclick="event.stopPropagation();showReplyBox(${threadId},${p.id})">↩️ Rispondi</button>`;
        if (currentUser.userId !== p.authorId) {
            actionsHtml += `<button onclick="event.stopPropagation();showModal('reputation',{userId:${p.authorId},username:'${escapeHtml(p.authorName).replace(/'/g, "\\'")}'})">⭐ Rep</button>`;
        }
    }
    if (canEdit) {
        actionsHtml += `<button onclick="event.stopPropagation();deletePost(${threadId},${p.id})">🗑️</button>`;
    }

    // Reactions bar
    let reactionsHtml = '';
    if (p.reactions && Object.keys(p.reactions).length > 0) {
        reactionsHtml = '<div class="post-reactions">';
        for (const [emoji, count] of Object.entries(p.reactions)) {
            const active = p.currentUserReactions && p.currentUserReactions.includes(emoji) ? ' active' : '';
            reactionsHtml += `<button class="reaction-btn${active}" onclick="event.stopPropagation();toggleReaction(${p.id},'${emoji}',${threadId})">${emoji} <span>${count}</span></button>`;
        }
        reactionsHtml += '</div>';
    }

    // Add reaction button
    let addReactionHtml = '';
    if (currentUser) {
        addReactionHtml = `<div class="add-reaction-wrap"><button class="add-reaction-btn" onclick="event.stopPropagation();toggleReactionPicker(${p.id},${threadId})">😀+</button>
            <div class="reaction-picker" id="reactionPicker-${p.id}" style="display:none">
                ${['👍','👎','❤️','😂','😮','😢','🔥','💀','🤔','👀','🎯','💯','⚡','🚀','💎'].map(e => `<button onclick="event.stopPropagation();toggleReaction(${p.id},'${e}',${threadId})">${e}</button>`).join('')}
            </div>
        </div>`;
    }

    // Attachments
    let attachmentsHtml = '';
    if (p.attachments && p.attachments.length > 0) {
        attachmentsHtml = '<div class="post-attachments"><span class="attachments-label">📎 Allegati:</span>';
        for (const att of p.attachments) {
            if (att.contentType && att.contentType.startsWith('image/')) {
                attachmentsHtml += `<div class="attachment-item attachment-image"><img src="${escapeHtml(att.url)}" alt="${escapeHtml(att.fileName)}" loading="lazy" onclick="window.open('${escapeHtml(att.url)}','_blank')"><span class="attachment-name">${escapeHtml(att.fileName)}</span></div>`;
            } else {
                attachmentsHtml += `<div class="attachment-item"><a href="${escapeHtml(att.url)}" target="_blank" rel="noopener noreferrer">📄 ${escapeHtml(att.fileName)}</a> <span class="attachment-size">(${formatFileSize(att.fileSizeBytes)})</span></div>`;
            }
        }
        attachmentsHtml += '</div>';
    }

    let html = `<div class="post fade-in" id="post-${p.id}">
        ${authorHtml}
        <div class="post-content-area">
            <div class="post-header">
                <span>${timeAgo(p.createdAt)}${edited}</span>
                <div class="post-actions">${actionsHtml}</div>
            </div>
            <div class="post-body">${formatContent(p.content)}</div>
            ${attachmentsHtml}
            <div class="post-footer-bar">${reactionsHtml}${addReactionHtml}</div>
            <div id="replyBox-${p.id}"></div>
        </div>
    </div>`;

    if (p.replies && p.replies.length > 0) {
        html += '<div style="margin-left:1.5rem">';
        html += p.replies.map(r => renderPost(r, threadId)).join('');
        html += '</div>';
    }
    return html;
}

function formatContent(text) {
    if (!text) return '';
    let s = escapeHtml(text);
    // BBCode
    s = s.replace(/\[b\]([\s\S]*?)\[\/b\]/gi, '<strong>$1</strong>');
    s = s.replace(/\[i\]([\s\S]*?)\[\/i\]/gi, '<em>$1</em>');
    s = s.replace(/\[u\]([\s\S]*?)\[\/u\]/gi, '<u>$1</u>');
    s = s.replace(/\[s\]([\s\S]*?)\[\/s\]/gi, '<s>$1</s>');
    s = s.replace(/\[color=([^\]]+)\]([\s\S]*?)\[\/color\]/gi, '<span style="color:$1">$2</span>');
    s = s.replace(/\[size=([^\]]+)\]([\s\S]*?)\[\/size\]/gi, '<span style="font-size:$1">$2</span>');
    s = s.replace(/\[url=([^\]]+)\]([\s\S]*?)\[\/url\]/gi, '<a href="$1" target="_blank" rel="noopener noreferrer">$2</a>');
    s = s.replace(/\[url\]([\s\S]*?)\[\/url\]/gi, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');
    s = s.replace(/\[img\]([\s\S]*?)\[\/img\]/gi, '<img src="$1" class="post-image" alt="img" loading="lazy">');
    s = s.replace(/\[quote\]([\s\S]*?)\[\/quote\]/gi, '<blockquote class="bb-quote">$1</blockquote>');
    s = s.replace(/\[quote=([^\]]+)\]([\s\S]*?)\[\/quote\]/gi, '<blockquote class="bb-quote"><cite>$1:</cite>$2</blockquote>');
    s = s.replace(/\[code\]([\s\S]*?)\[\/code\]/gi, '<pre><code>$1</code></pre>');
    s = s.replace(/\[spoiler\]([\s\S]*?)\[\/spoiler\]/gi, '<details class="spoiler"><summary>Spoiler</summary>$1</details>');
    s = s.replace(/\[list\]([\s\S]*?)\[\/list\]/gi, (m, inner) => '<ul>' + inner.replace(/\[\*\]/g, '<li>') + '</ul>');
    // Markdown — Code blocks
    s = s.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');
    // Inline code
    s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
    // Bold
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    // Italic
    s = s.replace(/\*(.+?)\*/g, '<em>$1</em>');
    // Strikethrough
    s = s.replace(/~~(.+?)~~/g, '<s>$1</s>');
    // Blockquote
    s = s.replace(/^&gt; (.+)$/gm, '<blockquote>$1</blockquote>');
    // Line breaks
    s = s.replace(/\n/g, '<br>');
    return s;
}

function showReplyBox(threadId, parentId) {
    const box = document.getElementById(`replyBox-${parentId}`);
    if (box.innerHTML) { box.innerHTML = ''; return; }
    box.innerHTML = `<div class="reply-box" style="margin-top:0.5rem">
        <textarea id="inlineReply-${parentId}" placeholder="Rispondi..." rows="3"></textarea>
        <button class="btn btn-primary btn-sm" onclick="submitInlineReply(${threadId},${parentId})">Invia</button>
    </div>`;
}

async function submitReply(threadId) {
    const content = document.getElementById('replyContent').value.trim();
    if (!content) return;
    try {
        await api(`/threads/${threadId}/posts`, {
            method: 'POST',
            body: JSON.stringify({ content, parentPostId: null })
        });
        loadThread(threadId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

async function submitInlineReply(threadId, parentId) {
    const content = document.getElementById(`inlineReply-${parentId}`).value.trim();
    if (!content) return;
    try {
        await api(`/threads/${threadId}/posts`, {
            method: 'POST',
            body: JSON.stringify({ content, parentPostId: parentId })
        });
        loadThread(threadId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

async function toggleLike(threadId, postId, btn) {
    if (!currentUser) { showModal('login'); return; }
    try {
        await api(`/threads/${threadId}/posts/${postId}/like`, { method: 'POST' });
        loadThread(threadId);
    } catch { /* ignore */ }
}

async function deletePost(threadId, postId) {
    if (!confirm('Eliminare questo post?')) return;
    try {
        await api(`/threads/${threadId}/posts/${postId}`, { method: 'DELETE' });
        loadThread(threadId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

// === Create Thread ===
async function createThread(e) {
    e.preventDefault();
    const errEl = document.getElementById('threadError');
    errEl.textContent = '';
    const tagId = document.getElementById('threadTag').value;
    try {
        const res = await api('/threads', {
            method: 'POST',
            body: JSON.stringify({
                title: document.getElementById('threadTitle').value.trim(),
                content: document.getElementById('threadContent').value.trim(),
                categoryId: parseInt(document.getElementById('threadCategory').value),
                tagId: tagId ? parseInt(tagId) : null,
                prefix: null
            })
        });
        closeModal();
        navigate('thread', { id: res.id });
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore nella creazione';
    }
}

// === Search ===
async function searchThreads() {
    const q = document.getElementById('searchInput').value.trim();
    if (!q || q.length < 2) return;
    navigate('search');
    loadSearchCategories();
    const container = document.getElementById('searchResults');
    container.innerHTML = '<div class="loading">Ricerca</div>';
    try {
        const res = await api('/search/advanced', {
            method: 'POST',
            body: JSON.stringify({ query: q, page: 1, pageSize: 20 })
        });
        if (!res.threads || res.threads.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">🔍</div><p>Nessun risultato</p></div>';
        } else {
            container.innerHTML = res.threads.map(t => threadCard(t)).join('');
        }
        renderSearchPagination(res.total, res.page, res.pageSize, q);
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore nella ricerca</p></div>';
    }
}

let searchCurrentPage = 1;

async function advancedSearch(page = 1) {
    const q = document.getElementById('searchInput').value.trim();
    const categoryId = document.getElementById('searchCategory').value || null;
    const tagId = document.getElementById('searchTag').value || null;
    const dateFrom = document.getElementById('searchDateFrom').value || null;
    const dateTo = document.getElementById('searchDateTo').value || null;
    
    const container = document.getElementById('searchResults');
    container.innerHTML = '<div class="loading">Ricerca avanzata...</div>';
    try {
        const res = await api('/search/advanced', {
            method: 'POST',
            body: JSON.stringify({ query: q || null, categoryId: categoryId ? parseInt(categoryId) : null, tagId: tagId ? parseInt(tagId) : null, dateFrom, dateTo, page, pageSize: 20 })
        });
        if (!res.threads || res.threads.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">🔍</div><p>Nessun risultato</p></div>';
        } else {
            container.innerHTML = res.threads.map(t => threadCard(t)).join('');
        }
        searchCurrentPage = page;
        renderSearchPagination(res.total, res.page, res.pageSize);
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore nella ricerca</p></div>';
    }
}

function renderSearchPagination(total, page, pageSize) {
    const pag = document.getElementById('searchPagination');
    if (!pag) return;
    const totalPages = Math.ceil(total / pageSize);
    if (totalPages <= 1) { pag.innerHTML = ''; return; }
    let html = '';
    if (page > 1) html += `<button onclick="advancedSearch(${page-1})">←</button>`;
    for (let i = 1; i <= totalPages; i++) {
        if (totalPages > 7 && Math.abs(i - page) > 2 && i !== 1 && i !== totalPages) {
            if (i === 2 || i === totalPages - 1) html += `<button disabled>...</button>`;
            continue;
        }
        html += `<button class="${i === page ? 'active' : ''}" onclick="advancedSearch(${i})">${i}</button>`;
    }
    if (page < totalPages) html += `<button onclick="advancedSearch(${page+1})">→</button>`;
    pag.innerHTML = html;
}

async function loadSearchCategories() {
    const sel = document.getElementById('searchCategory');
    if (sel && sel.options.length <= 1) {
        try {
            const cats = await api('/categories');
            const addOpts = (list, indent = 0) => {
                for (const c of list) {
                    const prefix = '—'.repeat(indent) + (indent ? ' ' : '');
                    sel.innerHTML += `<option value="${c.id}">${prefix}${c.icon} ${escapeHtml(c.name)}</option>`;
                    if (c.subCategories?.length) addOpts(c.subCategories, indent + 1);
                }
            };
            addOpts(cats);
        } catch { /* ignore */ }
    }
    const tagSel = document.getElementById('searchTag');
    if (tagSel && tagSel.options.length <= 1) {
        try {
            if (!cachedTags) cachedTags = await api('/leaderboard/tags');
            tagSel.innerHTML += cachedTags.map(t => `<option value="${t.id}">${t.name}</option>`).join('');
        } catch { /* ignore */ }
    }
}

// === Profile ===
async function loadProfile(id) {
    const container = document.getElementById('profileDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const [u, posts, repHistory] = await Promise.all([
            api(`/users/${id}`),
            api(`/users/${id}/posts`),
            api(`/reputation/${id}`)
        ]);
        const isOwnProfile = currentUser && currentUser.userId === u.id;
        const avatarHtml = u.avatarUrl
            ? `<img src="${escapeHtml(u.avatarUrl)}" alt="">`
            : u.username.charAt(0).toUpperCase();

        const statusIcon = {online:'🟢',away:'🟡',busy:'🔴',offline:'⚫'}[u.status] || '⚫';

        let html = `
            <span style="display:inline-block;margin-bottom:0.8rem;cursor:pointer;color:var(--accent);font-size:13px" onclick="navigate('home')">← Torna al forum</span>
            <div class="profile-header">
                <div class="profile-avatar">${avatarHtml}</div>
                <div class="profile-info">
                    <h2>${escapeHtml(u.username)} <span class="status-indicator" title="${u.status || 'offline'}">${statusIcon}</span></h2>
                    ${u.rankName ? `<span class="profile-rank" style="background:${u.rankColor||'#333'};color:#fff">${u.rankIcon||''} ${escapeHtml(u.rankName)}</span>` : ''}
                    ${u.customTitle ? `<div class="profile-custom-title">${escapeHtml(u.customTitle)}</div>` : ''}
                    <div class="profile-stats-row">
                        <span>📝 <strong>${u.threadCount}</strong> thread</span>
                        <span>💬 <strong>${u.postCount}</strong> post</span>
                        <span>⭐ <strong>${u.reputationScore}</strong> rep</span>
                        <span>💰 <strong>${u.credits}</strong> crediti</span>
                        <span>👥 <strong>${u.followerCount}</strong> follower</span>
                        <span>👤 <strong>${u.followingCount}</strong> seguiti</span>
                        <span>📅 Membro dal ${new Date(u.createdAt).toLocaleDateString('it-IT')}</span>
                    </div>
                    <div class="profile-actions">
                        ${isOwnProfile ? `<button class="btn btn-outline btn-sm" onclick="showModal('avatar')">📷 Avatar</button>
                            <button class="btn btn-outline btn-sm" onclick="toggleProfileEdit(${u.id})">✏️ Modifica</button>
                            <select class="status-select" onchange="updateUserStatus(this.value)">
                                <option value="online" ${u.status==='online'?'selected':''}>🟢 Online</option>
                                <option value="away" ${u.status==='away'?'selected':''}>🟡 Away</option>
                                <option value="busy" ${u.status==='busy'?'selected':''}>🔴 Busy</option>
                                <option value="offline" ${u.status==='offline'?'selected':''}>⚫ Offline</option>
                            </select>` : ''}
                        ${currentUser && !isOwnProfile ? `
                            <button class="btn btn-primary btn-sm" onclick="showModal('sendMessage',{userId:${u.id},username:'${escapeHtml(u.username).replace(/'/g, "\\'")}'})">✉️ Messaggio</button>
                            <button class="btn btn-outline btn-sm" onclick="showModal('reputation',{userId:${u.id},username:'${escapeHtml(u.username).replace(/'/g, "\\'")}'})">⭐ Rep</button>
                            <button class="btn ${u.followedByCurrentUser ? 'btn-danger' : 'btn-primary'} btn-sm" onclick="toggleFollow(${u.id})" id="followBtn">${u.followedByCurrentUser ? '❌ Smetti di seguire' : '➕ Segui'}</button>` : ''}
                    </div>
                </div>
            </div>`;

        if (u.bio) html += `<div class="profile-bio">${escapeHtml(u.bio)}</div>`;
        
        // Edit form (hidden by default) 
        if (isOwnProfile) {
            html += `<div class="profile-edit-form" id="profileEditForm" style="display:none">
                <label>Bio</label>
                <textarea id="editBio" rows="3">${escapeHtml(u.bio || '')}</textarea>
                <label>Firma</label>
                <input type="text" id="editSignature" value="${escapeHtml(u.signature || '')}" maxlength="200">
                <div style="margin-top:0.6rem;display:flex;gap:0.5rem">
                    <button class="btn btn-primary btn-sm" onclick="saveProfile(${u.id})">Salva</button>
                    <button class="btn btn-outline btn-sm" onclick="document.getElementById('profileEditForm').style.display='none'">Annulla</button>
                </div>
            </div>`;
        }

        // Tabs
        html += `<div class="profile-tabs">
            <button class="active" onclick="showProfileTab('posts',this)">📝 Post (${posts.length})</button>
            <button onclick="showProfileTab('reputation',this)">⭐ Reputazione (${repHistory.length})</button>
            <button onclick="showProfileTab('followers',this);loadFollowersTab(${u.id})">👥 Follower (${u.followerCount})</button>
            <button onclick="showProfileTab('following',this);loadFollowingTab(${u.id})">👤 Seguiti (${u.followingCount})</button>
        </div>`;

        // Posts tab
        html += `<div id="profileTabPosts" class="threads-list">`;
        if (posts.length === 0) {
            html += '<div class="empty-state"><p>Nessun post ancora</p></div>';
        } else {
            html += posts.map(p => `<div class="thread-card" onclick="navigate('thread',{id:${p.threadId}})">
                <div class="thread-card-body">
                    <div class="thread-card-title">${escapeHtml(p.threadTitle)}</div>
                    <div class="thread-card-meta">
                        <span>${escapeHtml(p.content.substring(0, 150))}${p.content.length > 150 ? '...' : ''}</span>
                        <span>${timeAgo(p.createdAt)}</span>
                    </div>
                </div>
            </div>`).join('');
        }
        html += '</div>';

        // Reputation tab
        html += `<div id="profileTabReputation" class="rep-list" style="display:none">`;
        if (repHistory.length === 0) {
            html += '<div class="empty-state"><p>Nessuna reputazione ricevuta</p></div>';
        } else {
            html += repHistory.map(r => `<div class="rep-item ${r.points > 0 ? 'positive' : 'negative'}">
                <span class="rep-points" style="color:${r.points > 0 ? 'var(--accent)' : 'var(--danger)'}">${r.points > 0 ? '+' : ''}${r.points}</span>
                <span class="rep-from" onclick="navigate('profile',{id:${r.giverId}})" style="cursor:pointer">${escapeHtml(r.giverName)}</span>
                <span class="rep-comment">${r.comment ? escapeHtml(r.comment) : ''}</span>
                <span class="rep-date">${timeAgo(r.createdAt)}</span>
            </div>`).join('');
        }
        html += '</div>';

        // Followers tab
        html += '<div id="profileTabFollowers" class="follow-list" style="display:none"><div class="loading">Caricamento...</div></div>';
        // Following tab
        html += '<div id="profileTabFollowing" class="follow-list" style="display:none"><div class="loading">Caricamento...</div></div>';

        container.innerHTML = html;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Utente non trovato</p></div>';
    }
}

function showProfileTab(tab, btn) {
    document.querySelectorAll('.profile-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('profileTabPosts').style.display = tab === 'posts' ? '' : 'none';
    document.getElementById('profileTabReputation').style.display = tab === 'reputation' ? '' : 'none';
    const ft = document.getElementById('profileTabFollowers');
    const fgt = document.getElementById('profileTabFollowing');
    if (ft) ft.style.display = tab === 'followers' ? '' : 'none';
    if (fgt) fgt.style.display = tab === 'following' ? '' : 'none';
}

function toggleProfileEdit(userId) {
    const form = document.getElementById('profileEditForm');
    form.style.display = form.style.display === 'none' ? '' : 'none';
}

async function saveProfile(userId) {
    try {
        await api(`/users/${userId}/profile`, {
            method: 'PUT',
            body: JSON.stringify({
                bio: document.getElementById('editBio').value.trim() || null,
                signature: document.getElementById('editSignature').value.trim() || null
            })
        });
        loadProfile(userId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

// === Reputation ===
function setRepPoints(val) {
    document.getElementById('repPoints').value = val;
    document.querySelectorAll('.rep-btn').forEach(b => b.classList.remove('active'));
    if (val === 1) document.querySelector('.rep-pos').classList.add('active');
    else document.querySelector('.rep-neg').classList.add('active');
}

async function submitReputation(e) {
    e.preventDefault();
    const errEl = document.getElementById('repError');
    errEl.textContent = '';
    try {
        await api('/reputation', {
            method: 'POST',
            body: JSON.stringify({
                userId: parseInt(document.getElementById('repUserId').value),
                points: parseInt(document.getElementById('repPoints').value),
                comment: document.getElementById('repComment').value.trim() || null
            })
        });
        closeModal();
        document.getElementById('repComment').value = '';
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore';
    }
}

// === Marketplace ===
async function loadMarketplace(type = null) {
    const grid = document.getElementById('marketplaceGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const params = new URLSearchParams();
        if (type) params.set('type', type);
        const listings = await api(`/marketplace?${params}`);
        
        // Filters
        const filtersContainer = document.getElementById('marketplaceFilters');
        filtersContainer.innerHTML = `
            <button class="filter-btn ${!type ? 'active' : ''}" onclick="loadMarketplace()">Tutti</button>
            <button class="filter-btn ${type==='Selling' ? 'active' : ''}" onclick="loadMarketplace('Selling')">🏷️ Vendita</button>
            <button class="filter-btn ${type==='Buying' ? 'active' : ''}" onclick="loadMarketplace('Buying')">🛒 Acquisto</button>
            <button class="filter-btn ${type==='Trading' ? 'active' : ''}" onclick="loadMarketplace('Trading')">🔄 Scambio</button>`;

        if (!listings || listings.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">🛒</div><p>Nessun annuncio</p></div>';
            return;
        }
        grid.innerHTML = listings.map(l => `<div class="marketplace-card">
            <div class="marketplace-card-header">
                <span class="marketplace-card-title">${escapeHtml(l.title)}</span>
                <span class="marketplace-card-price">💰 ${l.price}</span>
            </div>
            <span class="marketplace-card-type ${l.type.toLowerCase()}">${escapeHtml(l.type)}</span>
            <div class="marketplace-card-desc">${escapeHtml(l.description)}</div>
            <div class="marketplace-card-footer">
                <span class="marketplace-card-seller" onclick="navigate('profile',{id:${l.sellerId}})">${escapeHtml(l.sellerName)}</span>
                <span>${escapeHtml(l.categoryName)} · ${timeAgo(l.createdAt)}</span>
            </div>
        </div>`).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function createListing(e) {
    e.preventDefault();
    const errEl = document.getElementById('listingError');
    errEl.textContent = '';
    try {
        await api('/marketplace', {
            method: 'POST',
            body: JSON.stringify({
                title: document.getElementById('listingTitle').value.trim(),
                description: document.getElementById('listingDesc').value.trim(),
                price: parseInt(document.getElementById('listingPrice').value),
                type: document.getElementById('listingType').value,
                categoryId: parseInt(document.getElementById('listingCategory').value)
            })
        });
        closeModal();
        loadMarketplace();
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore';
    }
}

// === Leaderboard ===
async function loadLeaderboard(type) {
    const list = document.getElementById('leaderboardList');
    list.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const entries = await api(`/leaderboard/${type}`);
        if (!entries || entries.length === 0) {
            list.innerHTML = '<div class="empty-state"><p>Nessun dato</p></div>';
            return;
        }
        list.innerHTML = entries.map((e, i) => {
            const topClass = i === 0 ? 'top-1' : (i === 1 ? 'top-2' : (i === 2 ? 'top-3' : ''));
            const avatarHtml = e.avatarUrl ? `<img src="${escapeHtml(e.avatarUrl)}" alt="">` : e.username.charAt(0).toUpperCase();
            let value;
            if (type === 'reputation') value = `⭐ ${e.reputationScore}`;
            else if (type === 'posts') value = `📝 ${e.postCount}`;
            else value = `💰 ${e.credits}`;

            return `<div class="leaderboard-item ${topClass}">
                <span class="lb-rank">#${i + 1}</span>
                <div class="lb-avatar">${avatarHtml}</div>
                <span class="lb-name" onclick="navigate('profile',{id:${e.id}})">${escapeHtml(e.username)}</span>
                <span class="lb-value">${value}</span>
            </div>`;
        }).join('');
    } catch {
        list.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

function setLbTab(btn) {
    document.querySelectorAll('.leaderboard-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

// === Online Users (Members) ===
async function loadOnlineUsers() {
    const grid = document.getElementById('onlineUsersGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const users = await api('/leaderboard/online');
        if (!users || users.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">👥</div><p>Nessun utente online</p></div>';
            return;
        }
        grid.innerHTML = users.map(u => {
            const avatarHtml = u.avatarUrl ? `<img src="${escapeHtml(u.avatarUrl)}" alt="">` : u.username.charAt(0).toUpperCase();
            const statusIcon = {online:'🟢',away:'🟡',busy:'🔴',offline:'⚫'}[u.status] || '🟢';
            const chatBtn = currentUser && u.id !== currentUser.userId
                ? `<button class="btn btn-outline btn-xs chat-user-btn" onclick="event.stopPropagation();openChatWith(${u.id},'${escapeHtml(u.username).replace(/'/g, "\\'")}')">💬 Chat</button>`
                : '';
            return `<div class="online-user-card" onclick="navigate('profile',{id:${u.id}})">
                <div class="online-user-avatar">
                    ${avatarHtml}
                    <div class="online-indicator ${u.status || 'online'}"></div>
                </div>
                <div class="online-user-info">
                    <div class="online-user-name">${escapeHtml(u.username)} ${statusIcon}</div>
                    <div class="online-user-rank" style="color:${u.rankColor||'var(--text-muted)'}">${escapeHtml(u.rankName || '')}</div>
                </div>
                ${chatBtn}
            </div>`;
        }).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

// === Avatar ===
function loadAvatarPreview() {
    const preview = document.getElementById('avatarPreview');
    if (currentUser) {
        api('/auth/me').then(me => {
            if (me.avatarUrl) preview.innerHTML = `<img src="${escapeHtml(me.avatarUrl)}" alt="">`;
            else preview.innerHTML = `<span style="font-size:2rem;color:var(--accent)">${me.username.charAt(0).toUpperCase()}</span>`;
        }).catch(() => { preview.innerHTML = '📷'; });
    }
}

async function uploadAvatar(e) {
    e.preventDefault();
    const errEl = document.getElementById('avatarError');
    errEl.textContent = '';
    const fileInput = document.getElementById('avatarFile');
    if (!fileInput.files.length) { errEl.textContent = 'Seleziona un file'; return; }
    const formData = new FormData();
    formData.append('avatar', fileInput.files[0]);
    try {
        await apiRaw('/avatar/upload', { method: 'POST', body: formData });
        closeModal();
        refreshMe();
        if (document.getElementById('pageProfile').style.display !== 'none') loadProfile(currentUser.userId);
    } catch (err) { errEl.textContent = err.data?.error || 'Errore nel caricamento'; }
}

async function removeAvatar() {
    try {
        await api('/avatar', { method: 'DELETE' });
        closeModal();
        refreshMe();
        if (document.getElementById('pageProfile').style.display !== 'none') loadProfile(currentUser.userId);
    } catch { /* ignore */ }
}

// === Notifications ===
async function loadNotifications() {
    const container = document.getElementById('notificationsList');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const notifs = await api('/notifications');
        if (notifs.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">🔔</div><p>Nessuna notifica</p></div>';
            return;
        }
        container.innerHTML = notifs.map(n => {
            const iconMap = { Reply: '💬', Like: '❤️', Message: '✉️', Mention: '📣', System: '⚙️' };
            const icon = iconMap[n.type] || '🔔';
            const unread = n.isRead ? '' : ' unread';
            const clickAction = n.threadId ? `navigate('thread',{id:${n.threadId}})` : '';
            return `<div class="notification-item${unread}" onclick="${clickAction};markNotifRead(${n.id})">
                <span class="notification-icon">${icon}</span>
                <div class="notification-body">
                    <div class="notification-text">${escapeHtml(n.message)}</div>
                    <div class="notification-time">${timeAgo(n.createdAt)}${n.fromUsername ? ` · ${escapeHtml(n.fromUsername)}` : ''}</div>
                </div>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function markNotifRead(id) {
    try { await api(`/notifications/${id}/read`, { method: 'PUT' }); updateBadges(); } catch { /* ignore */ }
}
async function markAllNotificationsRead() {
    try { await api('/notifications/read-all', { method: 'PUT' }); updateBadges(); loadNotifications(); } catch { /* ignore */ }
}
async function deleteNotification(id) {
    try { await api(`/notifications/${id}`, { method: 'DELETE' }); loadNotifications(); updateBadges(); } catch { /* ignore */ }
}

// === Messages ===
async function loadConversations() {
    const container = document.getElementById('messagesContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const convos = await api('/messages/conversations');
        if (convos.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">✉️</div><p>Nessun messaggio</p></div>';
            return;
        }
        container.innerHTML = convos.map(c => {
            const avatarHtml = c.avatarUrl
                ? `<img src="${escapeHtml(c.avatarUrl)}" alt="">`
                : c.username.charAt(0).toUpperCase();
            const unread = c.unreadCount > 0 ? ' unread' : '';
            return `<div class="message-item${unread}" onclick="loadConversation(${c.userId})" style="cursor:pointer">
                <div class="message-avatar">${avatarHtml}</div>
                <div class="message-body">
                    <div class="message-from">${escapeHtml(c.username)}</div>
                    <div class="message-content">${escapeHtml(c.lastMessage)}</div>
                    <div class="message-time">${timeAgo(c.lastMessageAt)} ${c.unreadCount > 0 ? `· <strong>${c.unreadCount} non letti</strong>` : ''}</div>
                </div>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function loadConversation(otherId) {
    const container = document.getElementById('messagesContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const messages = await api(`/messages/${otherId}`);
        const otherName = messages.length > 0
            ? (messages[0].senderId === otherId ? messages[0].senderName : messages[0].receiverName)
            : 'Utente';

        let html = `<span style="cursor:pointer;color:var(--accent);font-size:13px" onclick="loadConversations()">← Conversazioni</span>
            <h3 style="margin:0.5rem 0 1rem;font-family:'Orbitron',sans-serif;font-size:16px">${escapeHtml(otherName)}</h3>
            <div style="display:flex;flex-direction:column;gap:0.4rem;margin-bottom:1rem">`;

        html += messages.map(m => {
            const isSent = m.senderId === currentUser.userId;
            return `<div class="message-item" style="border-left:3px solid ${isSent ? 'var(--accent-cyan)' : 'var(--accent)'}">
                <div class="message-body">
                    <div class="message-from" style="color:${isSent ? 'var(--accent-cyan)' : 'var(--accent)'}">${isSent ? 'Tu' : escapeHtml(m.senderName)}</div>
                    <div class="message-content">${escapeHtml(m.content)}</div>
                    <div class="message-time">${timeAgo(m.createdAt)}</div>
                </div>
            </div>`;
        }).join('');

        html += `</div>
            <div class="reply-box">
                <textarea id="chatInput" placeholder="Scrivi un messaggio..." rows="3" onkeydown="if(event.key==='Enter'&&!event.shiftKey){event.preventDefault();sendChatMessage(${otherId})}"></textarea>
                <button class="btn btn-primary" onclick="sendChatMessage(${otherId})">Invia</button>
            </div>`;
        container.innerHTML = html;
        updateBadges();
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function sendChatMessage(receiverId) {
    const input = document.getElementById('chatInput');
    const content = input.value.trim();
    if (!content) return;
    try {
        await api('/messages', { method: 'POST', body: JSON.stringify({ receiverId, content }) });
        loadConversation(receiverId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

async function sendMessageFromModal(e) {
    e.preventDefault();
    const errEl = document.getElementById('msgError');
    errEl.textContent = '';
    const receiverId = parseInt(document.getElementById('msgReceiverId').value);
    const content = document.getElementById('msgContent').value.trim();
    if (!content) { errEl.textContent = 'Scrivi un messaggio'; return; }
    try {
        await api('/messages', { method: 'POST', body: JSON.stringify({ receiverId, content }) });
        closeModal();
        document.getElementById('msgContent').value = '';
    } catch (err) { errEl.textContent = err.data?.error || 'Errore'; }
}

// === Admin Panel ===
let adminTab = 'stats';

async function loadAdmin() {
    const panel = document.getElementById('adminPanel');
    panel.innerHTML = `
        <h2>⚙️ Pannello Admin</h2>
        <div class="profile-tabs" style="margin-bottom:1rem">
            <button class="${adminTab === 'stats' ? 'active' : ''}" onclick="adminTab='stats';loadAdmin()">📊 Stats</button>
            <button class="${adminTab === 'users' ? 'active' : ''}" onclick="adminTab='users';loadAdmin()">👥 Utenti</button>
            <button class="${adminTab === 'threads' ? 'active' : ''}" onclick="adminTab='threads';loadAdmin()">📝 Thread</button>
        </div>
        <div id="adminContent"><div class="loading">Caricamento</div></div>`;

    const content = document.getElementById('adminContent');
    try {
        if (adminTab === 'stats') {
            const s = await api('/admin/stats');
            content.innerHTML = `
                <div class="admin-stats">
                    <div class="admin-stat"><div class="stat-value">${s.totalUsers}</div><div class="stat-label">Utenti</div></div>
                    <div class="admin-stat"><div class="stat-value">${s.totalThreads}</div><div class="stat-label">Thread</div></div>
                    <div class="admin-stat"><div class="stat-value">${s.totalPosts}</div><div class="stat-label">Post</div></div>
                    <div class="admin-stat"><div class="stat-value">${s.totalLikes}</div><div class="stat-label">Like</div></div>
                    <div class="admin-stat"><div class="stat-value">${s.onlineUsers}</div><div class="stat-label">Online</div></div>
                </div>
                <h3 style="margin:1rem 0 0.5rem;font-size:14px">Utenti recenti</h3>
                <div class="threads-list">
                    ${s.recentUsers.map(u => `<div class="thread-card" onclick="navigate('profile',{id:${u.id}})"><div class="thread-card-body"><div class="thread-card-title">${escapeHtml(u.username)}</div><div class="thread-card-meta"><span>${timeAgo(u.createdAt)}</span></div></div></div>`).join('')}
                </div>`;
        } else if (adminTab === 'users') {
            await loadAdminUsers(content);
        } else if (adminTab === 'threads') {
            await loadAdminThreads(content);
        }
    } catch (err) {
        content.innerHTML = err.status === 403
            ? '<div class="empty-state"><p>🚫 Accesso negato</p></div>'
            : '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function loadAdminUsers(container, search = '') {
    const params = new URLSearchParams({ page: 1, pageSize: 50 });
    if (search) params.set('search', search);
    const result = await api(`/admin/users?${params}`);

    container.innerHTML = `
        <div class="admin-search">
            <input type="text" id="adminUserSearch" placeholder="Cerca utente..." value="${escapeHtml(search)}" onkeydown="if(event.key==='Enter')searchAdminUsers()">
            <button class="btn btn-outline btn-sm" onclick="searchAdminUsers()">🔍</button>
        </div>
        ${result.users.map(u => `
            <div class="admin-user-card ${u.isBanned ? 'banned' : ''}">
                <div class="post-author-avatar" style="width:32px;height:32px;font-size:14px">${u.username.charAt(0).toUpperCase()}</div>
                <div class="admin-user-info">
                    <div class="admin-user-name">${escapeHtml(u.username)} ${u.isBanned ? '🚫' : ''}</div>
                    <div class="admin-user-meta">${escapeHtml(u.email)} · ${escapeHtml(u.role)} · 💰${u.credits} · ⭐${u.reputationScore}</div>
                </div>
                <div class="admin-user-actions">
                    <select onchange="changeUserRole(${u.id},this.value)" style="background:var(--bg-input);border:1px solid var(--border);color:var(--text-primary);border-radius:var(--radius-sm);padding:0.2rem 0.4rem;font-size:11px">
                        <option value="Member" ${u.role==='Member'?'selected':''}>Member</option>
                        <option value="Moderator" ${u.role==='Moderator'?'selected':''}>Moderator</option>
                        <option value="Admin" ${u.role==='Admin'?'selected':''}>Admin</option>
                    </select>
                    ${u.isBanned 
                        ? `<button class="btn btn-sm btn-outline" onclick="unbanUser(${u.id})">Sbanna</button>`
                        : `<button class="btn btn-sm btn-danger" onclick="banUser(${u.id},'${escapeHtml(u.username)}')">Ban</button>`}
                    <button class="btn btn-sm btn-outline" onclick="adminCredits(${u.id},'${escapeHtml(u.username)}')">💰</button>
                    <button class="btn btn-sm btn-danger" onclick="deleteUser(${u.id},'${escapeHtml(u.username)}')">🗑️</button>
                </div>
            </div>`).join('')}
        <p style="color:var(--text-muted);font-size:11px;margin-top:0.8rem">${result.total} utenti totali</p>`;
}

function searchAdminUsers() {
    loadAdminUsers(document.getElementById('adminContent'), document.getElementById('adminUserSearch').value.trim());
}

async function changeUserRole(userId, role) {
    try { await api(`/admin/users/${userId}/role`, { method: 'PUT', body: JSON.stringify({ role }) }); }
    catch (err) { alert(err.data?.error || 'Errore'); loadAdmin(); }
}

async function deleteUser(userId, username) {
    if (!confirm(`Eliminare l'utente ${username}?`)) return;
    try { await api(`/admin/users/${userId}`, { method: 'DELETE' }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function banUser(userId, username) {
    const reason = prompt(`Motivo ban per ${username}:`);
    if (reason === null) return;
    try { await api(`/admin/users/${userId}/ban`, { method: 'PUT', body: JSON.stringify({ reason }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function unbanUser(userId) {
    try { await api(`/admin/users/${userId}/unban`, { method: 'PUT' }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function adminCredits(userId, username) {
    const amount = prompt(`Crediti da aggiungere/rimuovere per ${username} (es: 100 o -50):`);
    if (!amount) return;
    const reason = prompt('Motivo:') || 'Admin adjustment';
    try { await api(`/admin/users/${userId}/credits`, { method: 'PUT', body: JSON.stringify({ amount: parseInt(amount), reason }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function loadAdminThreads(container, search = '') {
    const params = new URLSearchParams({ page: 1, pageSize: 50 });
    if (search) params.set('search', search);
    const result = await api(`/admin/threads?${params}`);

    container.innerHTML = `
        <div class="admin-search">
            <input type="text" id="adminThreadSearch" placeholder="Cerca thread..." value="${escapeHtml(search)}" onkeydown="if(event.key==='Enter')searchAdminThreads()">
            <button class="btn btn-outline btn-sm" onclick="searchAdminThreads()">🔍</button>
        </div>
        <div class="threads-list">
        ${result.threads.map(t => `
            <div class="thread-card">
                <div class="thread-card-body">
                    <div class="thread-card-title">${t.isPinned ? '📌 ' : ''}${t.isLocked ? '🔒 ' : ''}${escapeHtml(t.title)}</div>
                    <div class="thread-card-meta">
                        <span class="author">${escapeHtml(t.authorName)}</span>
                        <span class="category-badge">${escapeHtml(t.categoryName)}</span>
                        <span>${timeAgo(t.createdAt)}</span>
                        <span>💬 ${t.postCount}</span>
                    </div>
                </div>
                <div class="thread-card-stats" style="flex-direction:row;gap:0.3rem">
                    <button class="btn btn-sm ${t.isPinned ? 'btn-primary' : 'btn-outline'}" onclick="event.stopPropagation();togglePin(${t.id},${!t.isPinned})">📌</button>
                    <button class="btn btn-sm ${t.isLocked ? 'btn-primary' : 'btn-outline'}" onclick="event.stopPropagation();toggleLock(${t.id},${!t.isLocked})">🔒</button>
                    <button class="btn btn-sm btn-danger" onclick="event.stopPropagation();deleteThread(${t.id})">🗑️</button>
                </div>
            </div>`).join('')}
        </div>
        <p style="color:var(--text-muted);font-size:11px;margin-top:0.8rem">${result.total} thread totali</p>`;
}

function searchAdminThreads() {
    loadAdminThreads(document.getElementById('adminContent'), document.getElementById('adminThreadSearch').value.trim());
}

async function togglePin(threadId, pin) {
    try { await api(`/admin/threads/${threadId}`, { method: 'PUT', body: JSON.stringify({ isPinned: pin }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function toggleLock(threadId, lock) {
    try { await api(`/admin/threads/${threadId}`, { method: 'PUT', body: JSON.stringify({ isLocked: lock }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function deleteThread(threadId) {
    if (!confirm('Eliminare questo thread?')) return;
    try { await api(`/admin/threads/${threadId}`, { method: 'DELETE' }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

// === Utility ===
function escapeHtml(str) {
    if (!str) return '';
    const d = document.createElement('div');
    d.textContent = str;
    return d.innerHTML;
}

function timeAgo(dateStr) {
    if (!dateStr) return '';
    const now = new Date();
    const date = new Date(dateStr);
    const secs = Math.floor((now - date) / 1000);
    if (secs < 60) return 'ora';
    const mins = Math.floor(secs / 60);
    if (mins < 60) return `${mins}m fa`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h fa`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days}g fa`;
    const months = Math.floor(days / 30);
    if (months < 12) return `${months}me fa`;
    return `${Math.floor(months / 12)}a fa`;
}

function formatFileSize(bytes) {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

// === Reactions ===
async function toggleReaction(postId, emoji, threadId) {
    if (!currentUser) { showModal('login'); return; }
    try {
        await api(`/posts/${postId}/reactions`, {
            method: 'POST',
            body: JSON.stringify({ emoji })
        });
        showToast(`Reazione ${emoji} aggiunta!`);
    } catch (err) {
        if (err.status === 409) {
            await api(`/posts/${postId}/reactions/${encodeURIComponent(emoji)}`, { method: 'DELETE' });
            showToast(`Reazione ${emoji} rimossa`);
        }
    }
    loadThread(threadId);
}

function toggleReactionPicker(postId, threadId) {
    const picker = document.getElementById(`reactionPicker-${postId}`);
    if (picker) picker.style.display = picker.style.display === 'none' ? 'flex' : 'none';
}

// === Follow ===
async function toggleFollow(userId) {
    if (!currentUser) { showModal('login'); return; }
    const btn = document.getElementById('followBtn');
    try {
        if (btn && btn.textContent.includes('Smetti')) {
            await api(`/users/${userId}/follow`, { method: 'DELETE' });
            showToast('Non segui più questo utente');
        } else {
            await api(`/users/${userId}/follow`, { method: 'POST' });
            showToast('Ora segui questo utente!');
        }
        loadProfile(userId);
    } catch (err) {
        showToast(err.data?.error || 'Errore', 'error');
    }
}

async function loadFollowersTab(userId) {
    const container = document.getElementById('profileTabFollowers');
    if (!container) return;
    try {
        const followers = await api(`/users/${userId}/followers`);
        if (followers.length === 0) {
            container.innerHTML = '<div class="empty-state"><p>Nessun follower</p></div>';
        } else {
            container.innerHTML = followers.map(f => `<div class="follow-card" onclick="navigate('profile',{id:${f.id}})">
                <div class="follow-avatar">${f.avatarUrl ? `<img src="${escapeHtml(f.avatarUrl)}" alt="">` : f.username.charAt(0).toUpperCase()}</div>
                <div class="follow-info">
                    <span class="follow-name">${escapeHtml(f.username)}</span>
                    <span class="follow-rank" style="color:${f.rankColor || 'var(--text-muted)'}">${escapeHtml(f.rankName)}</span>
                </div>
                <span class="follow-date">${timeAgo(f.followedAt)}</span>
            </div>`).join('');
        }
    } catch { container.innerHTML = '<div class="empty-state"><p>Errore</p></div>'; }
}

async function loadFollowingTab(userId) {
    const container = document.getElementById('profileTabFollowing');
    if (!container) return;
    try {
        const following = await api(`/users/${userId}/following`);
        if (following.length === 0) {
            container.innerHTML = '<div class="empty-state"><p>Non segue nessuno</p></div>';
        } else {
            container.innerHTML = following.map(f => `<div class="follow-card" onclick="navigate('profile',{id:${f.id}})">
                <div class="follow-avatar">${f.avatarUrl ? `<img src="${escapeHtml(f.avatarUrl)}" alt="">` : f.username.charAt(0).toUpperCase()}</div>
                <div class="follow-info">
                    <span class="follow-name">${escapeHtml(f.username)}</span>
                    <span class="follow-rank" style="color:${f.rankColor || 'var(--text-muted)'}">${escapeHtml(f.rankName)}</span>
                </div>
                <span class="follow-date">${timeAgo(f.followedAt)}</span>
            </div>`).join('');
        }
    } catch { container.innerHTML = '<div class="empty-state"><p>Errore</p></div>'; }
}

// === User Status ===
async function updateUserStatus(status) {
    try {
        await api('/users/status', { method: 'PUT', body: JSON.stringify({ status }) });
        showToast(`Stato aggiornato: ${status}`);
    } catch { showToast('Errore nel cambio stato', 'error'); }
}

// === Quote ===
function quotePost(postId, authorName, content) {
    const replyBox = document.getElementById('replyContent');
    if (!replyBox) return;
    const quotedText = `[quote=${authorName}]${content}[/quote]\n`;
    replyBox.value = quotedText + replyBox.value;
    replyBox.focus();
    replyBox.scrollIntoView({ behavior: 'smooth' });
    showToast('Citazione aggiunta alla risposta');
}

// === WYSIWYG Toolbar ===
function wrapSelection(textareaId, before, after) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    const end = ta.selectionEnd;
    const selected = ta.value.substring(start, end);
    ta.value = ta.value.substring(0, start) + before + selected + after + ta.value.substring(end);
    ta.focus();
    ta.selectionStart = start + before.length;
    ta.selectionEnd = start + before.length + selected.length;
}

function insertBBCode(textareaId, type) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    let insert = '';
    if (type === 'url') {
        const url = prompt('Inserisci URL:');
        if (!url) return;
        const text = ta.value.substring(ta.selectionStart, ta.selectionEnd) || 'Link';
        insert = `[url=${url}]${text}[/url]`;
    } else if (type === 'img') {
        const url = prompt('Inserisci URL immagine:');
        if (!url) return;
        insert = `[img]${url}[/img]`;
    } else if (type === 'spoiler') {
        const selected = ta.value.substring(ta.selectionStart, ta.selectionEnd) || 'Contenuto nascosto';
        insert = `[spoiler]${selected}[/spoiler]`;
    }
    ta.value = ta.value.substring(0, start) + insert + ta.value.substring(ta.selectionEnd);
    ta.focus();
}

function showEmojiPicker(textareaId) {
    const emojis = ['😀','😂','🤣','😎','🤔','😮','😢','🔥','💀','👀','👍','👎','❤️','💯','⚡','🚀','💎','🎯','✅','❌'];
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const existing = document.getElementById('emojiPickerInline');
    if (existing) { existing.remove(); return; }
    const picker = document.createElement('div');
    picker.id = 'emojiPickerInline';
    picker.className = 'emoji-picker-inline';
    picker.innerHTML = emojis.map(e => `<button type="button" onclick="insertEmoji('${textareaId}','${e}')">${e}</button>`).join('');
    ta.parentElement.insertBefore(picker, ta);
}

function insertEmoji(textareaId, emoji) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    ta.value = ta.value.substring(0, start) + emoji + ta.value.substring(ta.selectionEnd);
    ta.focus();
    ta.selectionStart = ta.selectionEnd = start + emoji.length;
    const picker = document.getElementById('emojiPickerInline');
    if (picker) picker.remove();
}

// === Toast Notifications ===
function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `toast toast-${type} toast-enter`;
    toast.innerHTML = `<span class="toast-icon">${type === 'success' ? '✅' : type === 'error' ? '❌' : 'ℹ️'}</span><span>${escapeHtml(message)}</span>`;
    container.appendChild(toast);
    requestAnimationFrame(() => toast.classList.add('toast-visible'));
    setTimeout(() => {
        toast.classList.remove('toast-visible');
        toast.classList.add('toast-exit');
        setTimeout(() => toast.remove(), 400);
    }, 3000);
}

// === Theme Toggle ===
function toggleTheme() {
    const body = document.body;
    body.classList.toggle('light-theme');
    const isLight = body.classList.contains('light-theme');
    localStorage.setItem('crimecode_theme', isLight ? 'light' : 'dark');
    const btn = document.getElementById('themeToggle');
    if (btn) btn.textContent = isLight ? '☀️' : '🌙';
}

// Init theme from saved preference
(function initTheme() {
    const saved = localStorage.getItem('crimecode_theme');
    if (saved === 'light') {
        document.body.classList.add('light-theme');
        const btn = document.getElementById('themeToggle');
        if (btn) btn.textContent = '☀️';
    }
})();

// === Mobile Menu ===
function toggleMobileMenu() {
    const nav = document.getElementById('mainNav');
    const btn = document.getElementById('hamburgerBtn');
    nav.classList.toggle('open');
    btn.classList.toggle('active');
}

// Close mobile menu on link click
document.addEventListener('click', (e) => {
    if (e.target.closest('.main-nav .nav-link')) {
        const nav = document.getElementById('mainNav');
        const btn = document.getElementById('hamburgerBtn');
        if (nav) nav.classList.remove('open');
        if (btn) btn.classList.remove('active');
    }
});

// === Live Chat Widget ===
let chatCurrentUserId = null;
let chatLastMsgId = 0;
let chatPollInterval = null;

function toggleChatWidget() {
    const widget = document.getElementById('chatWidget');
    if (!widget) return;
    if (widget.style.display === 'none') {
        widget.style.display = 'flex';
        chatGoBack();
    } else {
        widget.style.display = 'none';
        stopChatPoll();
    }
}

function chatGoBack() {
    chatCurrentUserId = null;
    chatLastMsgId = 0;
    stopChatPoll();
    document.getElementById('chatWidgetTitle').textContent = '\ud83d\udcac Chat Live';
    document.getElementById('chatBackBtn').style.display = 'none';
    document.getElementById('chatWidgetInput').style.display = 'none';
    loadChatContacts();
}

async function loadChatContacts() {
    const body = document.getElementById('chatWidgetBody');
    body.innerHTML = '<div class="loading" style="padding:1rem">Caricamento...</div>';
    try {
        const contacts = await api('/chat/contacts');
        if (contacts.length === 0) {
            body.innerHTML = '<div class="empty-state" style="padding:1rem;text-align:center"><p style="color:var(--text-muted)">Nessun utente online</p></div>';
            return;
        }
        body.innerHTML = '<div class="chat-contacts-list">' + contacts.map(c => {
            const statusIcon = {online:'\ud83d\udfe2',away:'\ud83d\udfe1',busy:'\ud83d\udd34',offline:'\u26ab'}[c.status] || '\ud83d\udfe2';
            const avatarHtml = c.avatarUrl ? `<img src="${escapeHtml(c.avatarUrl)}" alt="">` : `<span>${c.username.charAt(0).toUpperCase()}</span>`;
            const unreadBadge = c.unreadCount > 0 ? `<span class="chat-contact-badge">${c.unreadCount}</span>` : '';
            const lastMsg = c.lastMessage ? `<div class="chat-contact-last">${escapeHtml(c.lastMessage)}</div>` : '';
            return `<div class="chat-contact-item" onclick="openChat(${c.userId},'${escapeHtml(c.username).replace(/'/g, "\\'")}')"> 
                <div class="chat-contact-avatar">${avatarHtml}</div>
                <div class="chat-contact-info">
                    <div class="chat-contact-name">${statusIcon} ${escapeHtml(c.username)} ${unreadBadge}</div>
                    ${lastMsg}
                </div>
            </div>`;
        }).join('') + '</div>';
    } catch {
        body.innerHTML = '<div class="empty-state" style="padding:1rem;text-align:center"><p style="color:var(--text-muted)">Errore</p></div>';
    }
}

async function openChat(userId, username) {
    chatCurrentUserId = userId;
    chatLastMsgId = 0;
    document.getElementById('chatWidgetTitle').textContent = escapeHtml(username);
    document.getElementById('chatBackBtn').style.display = '';
    document.getElementById('chatWidgetInput').style.display = 'flex';
    const body = document.getElementById('chatWidgetBody');
    body.innerHTML = '<div class="loading" style="padding:1rem">Caricamento...</div>';
    try {
        const msgs = await api(`/chat/${userId}`);
        renderChatMessages(msgs);
        startChatPoll();
        updateBadges();
    } catch {
        body.innerHTML = '<div class="empty-state" style="padding:1rem"><p>Errore</p></div>';
    }
    document.getElementById('chatMsgInput').focus();
}

function renderChatMessages(msgs) {
    const body = document.getElementById('chatWidgetBody');
    if (msgs.length === 0) {
        body.innerHTML = '<div class="chat-messages-empty"><p>Nessun messaggio. Scrivi per primo!</p></div>';
        return;
    }
    body.innerHTML = '<div class="chat-messages">' + msgs.map(m => {
        const isMine = m.senderId === currentUser.userId;
        chatLastMsgId = Math.max(chatLastMsgId, m.id);
        return `<div class="chat-msg ${isMine ? 'chat-msg-mine' : 'chat-msg-other'}">
            <div class="chat-msg-bubble">${escapeHtml(m.content)}</div>
            <div class="chat-msg-time">${timeAgo(m.createdAt)}</div>
        </div>`;
    }).join('') + '</div>';
    body.scrollTop = body.scrollHeight;
}

async function sendChatFromWidget() {
    if (!chatCurrentUserId) return;
    const input = document.getElementById('chatMsgInput');
    const content = input.value.trim();
    if (!content) return;
    input.value = '';
    try {
        const msg = await api(`/chat/${chatCurrentUserId}`, {
            method: 'POST',
            body: JSON.stringify({ content })
        });
        // Append this message to the chat
        const body = document.getElementById('chatWidgetBody');
        const container = body.querySelector('.chat-messages');
        if (container) {
            const div = document.createElement('div');
            div.className = 'chat-msg chat-msg-mine';
            div.innerHTML = `<div class="chat-msg-bubble">${escapeHtml(msg.content)}</div><div class="chat-msg-time">ora</div>`;
            container.appendChild(div);
            chatLastMsgId = Math.max(chatLastMsgId, msg.id);
            body.scrollTop = body.scrollHeight;
        } else {
            // First message in empty chat
            renderChatMessages([msg]);
        }
    } catch (err) {
        showToast(err.data?.error || 'Errore invio messaggio', 'error');
    }
}

function startChatPoll() {
    stopChatPoll();
    chatPollInterval = setInterval(async () => {
        if (!chatCurrentUserId) return;
        try {
            const newMsgs = await api(`/chat/${chatCurrentUserId}/new?afterId=${chatLastMsgId}`);
            if (newMsgs.length > 0) {
                const body = document.getElementById('chatWidgetBody');
                let container = body.querySelector('.chat-messages');
                if (!container) {
                    body.innerHTML = '<div class="chat-messages"></div>';
                    container = body.querySelector('.chat-messages');
                }
                for (const m of newMsgs) {
                    if (m.senderId === currentUser.userId) continue; // already shown
                    chatLastMsgId = Math.max(chatLastMsgId, m.id);
                    const div = document.createElement('div');
                    div.className = 'chat-msg chat-msg-other';
                    div.innerHTML = `<div class="chat-msg-bubble">${escapeHtml(m.content)}</div><div class="chat-msg-time">ora</div>`;
                    container.appendChild(div);
                }
                body.scrollTop = body.scrollHeight;
                updateBadges();
            }
        } catch { /* ignore */ }
    }, 3000);
}

function stopChatPoll() {
    if (chatPollInterval) {
        clearInterval(chatPollInterval);
        chatPollInterval = null;
    }
}

// Show chat toggle button when logged in
function updateChatVisibility() {
    const btn = document.getElementById('chatToggleBtn');
    if (btn) btn.style.display = currentUser ? '' : 'none';
    if (!currentUser) {
        const widget = document.getElementById('chatWidget');
        if (widget) widget.style.display = 'none';
        stopChatPoll();
    }
}

function openChatWith(userId, username) {
    const widget = document.getElementById('chatWidget');
    if (widget) widget.style.display = 'flex';
    openChat(userId, username);
}
