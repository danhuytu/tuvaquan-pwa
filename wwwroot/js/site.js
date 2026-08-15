(() => {
  const $ = selector => document.querySelector(selector);
  let currentUser = null, pendingAvatar = null, pendingBackground = null, knownUnreadIds = new Set();
  function createPetals() {
    const layer = $('#petalLayer');
    if (layer.childElementCount) return;
    for (let index = 0; index < 24; index++) {
      const petal = document.createElement('i');
      petal.className = 'petal';
      petal.style.left = `${Math.random() * 100}%`;
      petal.style.setProperty('--fall-duration', `${7 + Math.random() * 8}s`);
      petal.style.setProperty('--sway-duration', `${1.8 + Math.random() * 2}s`);
      petal.style.setProperty('--fall-delay', `${-Math.random() * 12}s`);
      petal.style.transform = `rotate(${Math.random() * 360}deg)`;
      layer.append(petal);
    }
  }
  function setTheme(isDark) {
    document.body.classList.toggle('dark-mode', isDark);
    $('#petalLayer').classList.toggle('is-hidden', !isDark);
    $('#themeToggle').textContent = isDark ? '☀' : '☾';
    $('#themeToggle').setAttribute('aria-label', isDark ? 'Bật giao diện sáng' : 'Bật giao diện tối');
    localStorage.setItem('tu-va-quan-theme', isDark ? 'dark' : 'light');
    if (isDark) createPetals();
  }
  async function api(url, options = {}) {
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const needsCsrf = !['GET', 'HEAD', 'OPTIONS'].includes((options.method || 'GET').toUpperCase());
    const response = await fetch(url, { credentials: 'same-origin', headers: { 'Content-Type': 'application/json', ...(needsCsrf ? { 'RequestVerificationToken': antiForgeryToken } : {}), ...(options.headers || {}) }, ...options });
    if (response.status === 204) return null;
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.message || 'Có lỗi xảy ra. Vui lòng thử lại.');
    return body;
  }
  const toast = message => { const node = document.createElement('div'); node.className = 'toast'; node.textContent = message; document.body.append(node); setTimeout(() => node.remove(), 3400); };
  const escapeHtml = value => { const el = document.createElement('div'); el.textContent = value || ''; return el.innerHTML; };
  const formatDate = value => {
    const timestamp = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
    return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(timestamp));
  };
  const normalized = value => value.trim().toLocaleLowerCase('vi-VN');
  const urlBase64ToUint8Array = value => {
    const padding = '='.repeat((4 - value.length % 4) % 4);
    const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
    return Uint8Array.from(atob(base64), character => character.charCodeAt(0));
  };
  async function registerPushSubscription(showFeedback = false) {
    if (!currentUser || !('PushManager' in window) || !('serviceWorker' in navigator)) return false;
    if (!window.isSecureContext && location.hostname !== 'localhost') throw new Error('Thông báo chỉ hoạt động khi web chạy bằng HTTPS.');
    const permission = Notification.permission === 'default' ? await Notification.requestPermission() : Notification.permission;
    if (permission !== 'granted') throw new Error('Thông báo đang bị chặn. Hãy cho phép trong cài đặt trình duyệt.');
    const registration = await navigator.serviceWorker.ready;
    let subscription = await registration.pushManager.getSubscription();
    if (!subscription) {
      const { publicKey } = await api('/api/notifications/vapid-public-key');
      subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(publicKey) });
    }
    await api('/api/notifications/subscribe', { method: 'POST', body: JSON.stringify(subscription.toJSON()) });
    if (showFeedback) {
      await registration.showNotification('Tú và Quân', { body: 'Đã bật thông báo thư mới.', icon: '/images/icon-192.png' });
      toast('Đã bật thông báo, kể cả khi bạn đóng app.');
    }
    return true;
  }
  async function unregisterPushSubscription() {
    if (!('serviceWorker' in navigator)) return;
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    if (!subscription) return;
    await api('/api/notifications/unsubscribe', { method: 'POST', body: JSON.stringify({ endpoint: subscription.endpoint }) });
    await subscription.unsubscribe();
  }
  async function unreadLetters() { return api('/api/letters?folder=received&unreadOnly=true'); }
  async function updateCounts() {
    const letters = await unreadLetters();
    $('#loveCount').textContent = `${letters.filter(letter => letter.type === 'love').length} thư mới`;
    $('#roastCount').textContent = `${letters.filter(letter => letter.type === 'roast').length} thư mới`;
    return letters;
  }
  async function refreshShell() {
    try { currentUser = await api('/api/auth/me'); } catch { currentUser = null; }
    $('#authScreen').classList.toggle('is-hidden', !!currentUser); $('#mailScreen').classList.toggle('is-hidden', !currentUser);
    if (!currentUser) return;
    $('#profileName').textContent = currentUser.displayName || currentUser.username;
    $('#profileAvatar').innerHTML = currentUser.avatarDataUrl ? `<img src="${currentUser.avatarDataUrl}" alt="Ảnh đại diện">` : `<b>${escapeHtml((currentUser.displayName || currentUser.username)[0].toUpperCase())}</b>`;
    $('#adminMenuItem').classList.toggle('is-hidden', !currentUser.isAdmin);
    renderMailboxImages();
    renderBackground();
    await updateCounts();
    if (Notification.permission === 'granted') registerPushSubscription().catch(() => {});
  }
  function renderBackground() {
    const source = currentUser.backgroundImage;
    const layer = $('#backgroundLayer');
    layer.style.backgroundImage = source ? `linear-gradient(135deg, #e85f8050, #160d1050), url("${source}")` : '';
    layer.classList.toggle('is-hidden', !source);
  }
  function renderMailboxImages() {
    [['love', currentUser.loveMailboxImage], ['roast', currentUser.roastMailboxImage]].forEach(([type, source]) => {
      const image = $(`#${type}MailboxImage`);
      image.src = source || ''; image.classList.toggle('is-hidden', !source);
      document.querySelector(`[data-clear-mailbox="${type}"]`).classList.toggle('is-hidden', !source);
    });
  }
  function openModal(view) { $('#modal').classList.remove('is-hidden'); ['composeView', 'inboxView', 'editProfileView', 'adminView'].forEach(id => $(`#${id}`).classList.toggle('is-hidden', id !== `${view}View`)); }
  function closeModal() { $('#modal').classList.add('is-hidden'); }
  function activityStatus(lastActiveAt) {
    const timestamp = /(?:Z|[+-]\d{2}:\d{2})$/i.test(lastActiveAt) ? lastActiveAt : `${lastActiveAt}Z`;
    return Date.now() - new Date(timestamp).getTime() < 5 * 60 * 1000 ? 'Đang hoạt động' : `Hoạt động: ${formatDate(lastActiveAt)}`;
  }
  async function openAdminPanel() {
    openModal('admin'); $('#adminUserList').innerHTML = '<p class="empty-state">Đang tải...</p>';
    try {
      const users = await api('/api/admin/users');
      $('#adminUserList').innerHTML = users.map(user => `<article class="admin-user"><div><strong>${escapeHtml(user.displayName)} ${user.isAdmin ? '· Admin' : ''}</strong><small>@${escapeHtml(user.username)} · Tham gia: ${formatDate(user.createdAt)}</small><small class="admin-status ${user.isBanned ? 'is-banned' : ''}">${user.isBanned ? 'Đã bị khóa' : activityStatus(user.lastActiveAt)}</small></div>${user.isAdmin ? '' : `<button class="ban-button" data-ban-user="${user.id}" data-ban-state="${!user.isBanned}">${user.isBanned ? 'Mở khóa' : 'Khóa tài khoản'}</button>`}</article>`).join('') || '<p class="empty-state">Chưa có người dùng.</p>';
      document.querySelectorAll('[data-ban-user]').forEach(button => button.addEventListener('click', async () => {
        try { await api(`/api/admin/users/${button.dataset.banUser}/ban`, { method: 'PUT', body: JSON.stringify({ isBanned: button.dataset.banState === 'true' }) }); await openAdminPanel(); } catch (error) { toast(error.message); }
      }));
    } catch (error) { $('#adminUserList').innerHTML = `<p class="empty-state">${escapeHtml(error.message)}</p>`; }
  }
  function renderLetters(letters, sent, emptyText) {
    $('#letterList').innerHTML = letters.length ? letters.map(letter => {
      const name = sent ? letter.toDisplayName : letter.fromDisplayName;
      const avatar = sent ? letter.toAvatarDataUrl : letter.fromAvatarDataUrl;
      const fallback = escapeHtml((name || '?')[0].toUpperCase());
      const picture = avatar ? `<img src="${avatar}" alt="Ảnh đại diện của ${escapeHtml(name)}">` : `<b>${fallback}</b>`;
      const state = sent ? (letter.readAt ? `Đã đọc · ${formatDate(letter.readAt)}` : 'Chưa đọc') : (letter.type === 'love' ? 'Tiếng yêu' : 'Tao đang chửi mày đây, vô mà đọc');
      return `<article class="letter"><header><span class="letter-person"><i class="letter-avatar">${picture}</i><span>${sent ? 'Gửi đến' : 'Từ'}: ${escapeHtml(name)}<small>${state}</small></span></span><time>${formatDate(letter.createdAt)}</time></header><p>${escapeHtml(letter.content)}</p></article>`;
    }).join('') : `<p class="empty-state">${emptyText}</p>`;
  }
  async function openInbox(type) {
    openModal('inbox'); $('#inboxTitle').textContent = type === 'love' ? 'Tiếng yêu này anh dịch được không?' : 'Tao đang chửi mày đây, vô mà đọc';
    try {
      const letters = await api(`/api/letters?folder=received&type=${type}`); renderLetters(letters, false, 'Hòm thư này chưa có lá thư nào.');
      const unreadIds = letters.filter(letter => !letter.isRead).map(letter => letter.id);
      if (unreadIds.length) await api('/api/letters/mark-read', { method: 'POST', body: JSON.stringify({ ids: unreadIds }) });
      await updateCounts();
    } catch (error) { toast(error.message); }
  }
  async function openHistory(kind) {
    openModal('inbox'); const sent = kind === 'sent'; $('#inboxTitle').textContent = sent ? 'Thư đã gửi' : 'Tất cả thư đã nhận';
    try { const letters = await api(`/api/letters?folder=${sent ? 'sent' : 'received'}`); renderLetters(letters, sent, sent ? 'Bạn chưa gửi lá thư nào.' : 'Bạn chưa nhận được lá thư nào.'); } catch (error) { toast(error.message); }
  }
  async function showLetterNotification(letter) {
    const sender = letter.fromDisplayName || letter.from;
    const message = letter.type === 'roast' ? `${sender} đang chửi vào mặt mày` : `${sender} gửi lời yêu thương đến bạn`;
    toast(message); if (Notification.permission !== 'granted') return;
    const registration = await navigator.serviceWorker?.ready; if (registration) registration.showNotification('Tú và Quân', { body: message, icon: '/images/icon-192.png' });
  }
  async function pollForNewLetters() {
    if (!currentUser) return;
    try { const letters = await updateCounts(); for (const letter of letters) if (!knownUnreadIds.has(letter.id)) await showLetterNotification(letter); knownUnreadIds = new Set(letters.map(letter => letter.id)); } catch { }
  }
  document.querySelectorAll('[data-auth-tab]').forEach(tab => tab.addEventListener('click', () => { const login = tab.dataset.authTab === 'login'; document.querySelectorAll('[data-auth-tab]').forEach(item => item.classList.toggle('is-active', item === tab)); $('#loginForm').classList.toggle('is-hidden', !login); $('#registerForm').classList.toggle('is-hidden', login); }));
  $('#registerForm').addEventListener('submit', async event => { event.preventDefault(); $('#registerMessage').textContent = ''; try { await api('/api/auth/register', { method: 'POST', body: JSON.stringify({ displayName: $('#registerDisplayName').value.trim(), username: normalized($('#registerUsername').value), password: $('#registerPassword').value }) }); $('#registerMessage').style.color = '#438258'; $('#registerMessage').textContent = 'Tạo tài khoản thành công. Bạn hãy đăng nhập nhé!'; event.target.reset(); } catch (error) { $('#registerMessage').style.color = ''; $('#registerMessage').textContent = error.message; } });
  $('#loginForm').addEventListener('submit', async event => { event.preventDefault(); $('#loginMessage').textContent = ''; try { await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ username: normalized($('#loginUsername').value), password: $('#loginPassword').value, loveAnswer: $('#loveAnswer').value }) }); event.target.reset(); await refreshShell(); knownUnreadIds = new Set((await unreadLetters()).map(letter => letter.id)); } catch (error) { $('#loginMessage').textContent = error.message; } });
  $('#composeButton').addEventListener('click', () => { $('#composeForm').reset(); $('#composeMessage').textContent = ''; openModal('compose'); });
  $('#composeForm').addEventListener('submit', async event => { event.preventDefault(); try { await api('/api/letters', { method: 'POST', body: JSON.stringify({ recipient: normalized($('#recipient').value), type: $('#letterType').value, content: $('#letterContent').value.trim() }) }); $('#composeMessage').style.color = '#438258'; $('#composeMessage').textContent = 'Lá thư đã được gửi đi.'; setTimeout(closeModal, 700); } catch (error) { $('#composeMessage').style.color = ''; $('#composeMessage').textContent = error.message; } });
  document.querySelectorAll('[data-mailbox-upload]').forEach(input => input.addEventListener('change', event => {
    const file = event.target.files[0]; if (!file) return;
    if (file.size > 1_500_000) { toast('Mỗi ảnh cần nhỏ hơn 1.5 MB.'); event.target.value = ''; return; }
    const reader = new FileReader(); reader.onload = async () => {
      const type = input.dataset.mailboxUpload;
      try {
        currentUser = await api('/api/auth/profile', { method: 'PUT', body: JSON.stringify({ displayName: currentUser.displayName, avatarDataUrl: currentUser.avatarDataUrl, loveMailboxImage: type === 'love' ? reader.result : currentUser.loveMailboxImage, roastMailboxImage: type === 'roast' ? reader.result : currentUser.roastMailboxImage }) });
        renderMailboxImages(); toast('Đã lưu ảnh cho hòm thư.');
      } catch (error) { toast(error.message); }
      event.target.value = '';
    }; reader.readAsDataURL(file);
  }));
  document.querySelectorAll('[data-clear-mailbox]').forEach(button => button.addEventListener('click', async () => {
    const type = button.dataset.clearMailbox;
    try {
      currentUser = await api('/api/auth/profile', { method: 'PUT', body: JSON.stringify({ displayName: currentUser.displayName, avatarDataUrl: currentUser.avatarDataUrl, loveMailboxImage: type === 'love' ? '' : currentUser.loveMailboxImage, roastMailboxImage: type === 'roast' ? '' : currentUser.roastMailboxImage }) });
      renderMailboxImages(); toast('Đã xoá ảnh hòm thư.');
    } catch (error) { toast(error.message); }
  }));
  document.querySelectorAll('[data-mailbox]').forEach(box => box.addEventListener('click', () => openInbox(box.dataset.mailbox)));
  document.querySelectorAll('[data-history]').forEach(button => button.addEventListener('click', () => openHistory(button.dataset.history)));
  $('#closeModal').addEventListener('click', closeModal); $('#modal').addEventListener('click', event => { if (event.target === $('#modal')) closeModal(); });
  $('#profileButton').addEventListener('click', () => $('#profileMenu').classList.toggle('is-hidden'));
  document.addEventListener('click', event => { if (!event.target.closest('.header-actions')) $('#profileMenu').classList.add('is-hidden'); });
  document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', async () => {
    $('#profileMenu').classList.add('is-hidden');
    if (button.dataset.action === 'logout') { try { await unregisterPushSubscription(); } catch { } await api('/api/auth/logout', { method: 'POST' }); currentUser = null; await refreshShell(); return; }
    if (button.dataset.action === 'admin') { await openAdminPanel(); return; }
    pendingAvatar = null; pendingBackground = null; $('#displayName').value = currentUser.displayName || ''; $('#avatarUpload').value = ''; $('#avatarPreview').classList.toggle('is-hidden', !currentUser.avatarDataUrl); $('#avatarPreview').src = currentUser.avatarDataUrl || ''; $('#backgroundUpload').value = ''; $('#backgroundPreview').classList.toggle('is-hidden', !currentUser.backgroundImage); $('#backgroundPreview').style.backgroundImage = currentUser.backgroundImage ? `url("${currentUser.backgroundImage}")` : ''; $('#clearBackground').classList.toggle('is-hidden', !currentUser.backgroundImage); $('#profileMessage').textContent = ''; openModal('editProfile');
  }));
  $('#avatarUpload').addEventListener('change', event => { const file = event.target.files[0]; if (!file) return; if (file.size > 1_500_000) { $('#profileMessage').textContent = 'Ảnh cần nhỏ hơn 1.5 MB.'; event.target.value = ''; return; } const reader = new FileReader(); reader.onload = () => { pendingAvatar = reader.result; $('#avatarPreview').src = pendingAvatar; $('#avatarPreview').classList.remove('is-hidden'); }; reader.readAsDataURL(file); });
  $('#backgroundUpload').addEventListener('change', event => { const file = event.target.files[0]; if (!file) return; if (file.size > 1_500_000) { $('#profileMessage').textContent = 'Ảnh cần nhỏ hơn 1.5 MB.'; event.target.value = ''; return; } const reader = new FileReader(); reader.onload = () => { pendingBackground = reader.result; $('#backgroundPreview').style.backgroundImage = `url("${pendingBackground}")`; $('#backgroundPreview').classList.remove('is-hidden'); $('#clearBackground').classList.remove('is-hidden'); }; reader.readAsDataURL(file); });
  $('#clearBackground').addEventListener('click', () => { pendingBackground = ''; $('#backgroundUpload').value = ''; $('#backgroundPreview').classList.add('is-hidden'); $('#clearBackground').classList.add('is-hidden'); });
  $('#profileForm').addEventListener('submit', async event => { event.preventDefault(); try { currentUser = await api('/api/auth/profile', { method: 'PUT', body: JSON.stringify({ displayName: $('#displayName').value.trim(), avatarDataUrl: pendingAvatar, backgroundImage: pendingBackground }) }); $('#profileMessage').style.color = '#438258'; $('#profileMessage').textContent = 'Đã lưu thông tin.'; await refreshShell(); } catch (error) { $('#profileMessage').style.color = ''; $('#profileMessage').textContent = error.message; } });
  $('#notificationButton').addEventListener('click', async () => { if (!('Notification' in window)) return toast('Trình duyệt này chưa hỗ trợ thông báo.'); try { await registerPushSubscription(true); } catch (error) { toast(error.message || 'Không thể bật thông báo. Hãy kiểm tra quyền thông báo của trình duyệt.'); } });
  $('#themeToggle').addEventListener('click', () => setTheme(!document.body.classList.contains('dark-mode')));
  if ('serviceWorker' in navigator) window.addEventListener('load', async () => { await navigator.serviceWorker.register('/sw.js'); await navigator.serviceWorker.getRegistration().then(registration => registration?.update()); });
  setTheme(localStorage.getItem('tu-va-quan-theme') === 'dark');
  refreshShell().then(async () => { if (currentUser) knownUnreadIds = new Set((await unreadLetters()).map(letter => letter.id)); }); setInterval(pollForNewLetters, 20_000);
})();
