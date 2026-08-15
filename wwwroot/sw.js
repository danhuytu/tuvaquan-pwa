// Không lưu giao diện trong cache: người dùng luôn nhận được bản mới nhất.
const CACHE = 'tu-va-quan-v19';

self.addEventListener('install', event => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const names = await caches.keys();
    await Promise.all(names.filter(name => name !== CACHE).map(name => caches.delete(name)));
    await self.clients.claim();
    // Chuyển mọi tab đang dùng giao diện cũ sang bản mới ngay sau khi cập nhật.
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    await Promise.all(clients.map(client => client.navigate(client.url)));
  })());
});

self.addEventListener('push', event => {
  const data = event.data ? event.data.json() : {};
  event.waitUntil(self.registration.showNotification(data.title || 'Tú và Quân', {
    body: data.body || 'Bạn có thư mới.',
    icon: data.icon || '/images/icon-192.png',
    badge: '/images/icon-192.png',
    tag: 'tu-va-quan-letter',
    data: { url: data.url || '/' }
  }));
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil((async () => {
    const target = new URL(event.notification.data?.url || '/', self.location.origin).href;
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const existing = clients.find(client => client.url === target || client.url.startsWith(self.location.origin));
    if (existing) return existing.focus();
    return self.clients.openWindow(target);
  })());
});
