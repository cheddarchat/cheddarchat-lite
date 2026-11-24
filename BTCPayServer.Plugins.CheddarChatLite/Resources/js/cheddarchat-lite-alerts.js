'use strict'

document.addEventListener('DOMContentLoaded', () => {
    // Collapse bursts of changes (e.g. several CheddarChats at once) into a single refresh
    const REFRESH_DEBOUNCE_MS = 500
    const region = document.getElementById('CheddarChatAlerts')
    if (!region) return

    // Save the auto play setting as soon as it gets toggled
    region.addEventListener('change', e => {
        if (e.target.matches('[data-autosubmit]')) e.target.form.requestSubmit()
    })

    // Live update the donations list and overlay status
    let refreshTimer = null
    let refreshing = false
    let refreshAgain = false
    function scheduleRefresh() {
        if (!refreshTimer) refreshTimer = setTimeout(refresh, REFRESH_DEBOUNCE_MS)
    }
    async function refresh() {
        refreshTimer = null
        if (refreshing) {
            refreshAgain = true
            return
        }
        refreshing = true
        try {
            // Only fetch the live-updating part of the page, not the whole layout
            const response = await fetch(window.location.href, {
                cache: 'no-store',
                credentials: 'same-origin',
                headers: {
                    'Accept': 'text/html',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            })
            // The session expired or the access got revoked: reloading leads to the login or error page
            if (response.status === 401 || response.status === 403) return window.location.reload()
            if (response.ok) {
                const template = document.createElement('template')
                template.innerHTML = await response.text()
                region.replaceChildren(template.content)
            }
        } catch (err) {
            console.warn('Could not refresh the alerts', err)
        } finally {
            refreshing = false
            if (refreshAgain) {
                refreshAgain = false
                scheduleRefresh()
            }
        }
    }

    let retries = 0
    function connect() {
        const url = new URL(region.dataset.socketUrl, window.location.href)
        url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'
        const socket = new WebSocket(url)
        socket.addEventListener('open', () => {
            // Catch up on changes that happened while disconnected
            if (retries > 0) scheduleRefresh()
            retries = 0
        })
        socket.addEventListener('message', ({ data }) => {
            if (data === 'refresh') scheduleRefresh()
        })
        socket.addEventListener('close', () => {
            setTimeout(connect, Math.min(30000, 1000 * 2 ** retries++))
        })
    }
    connect()
})
