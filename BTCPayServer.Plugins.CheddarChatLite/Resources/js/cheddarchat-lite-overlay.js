'use strict'

;(function () {
    const FADE_MS = 500
    const SPEAK_DELAY_MS = 600
    const MAX_ALERT_MS = 120000
    // Generous estimate of how long reading a message takes (default voices speak ~15 characters per second)
    const SPEECH_BASE_MS = 5000
    const SPEECH_MS_PER_CHAR = 90
    // The server pings every 25 seconds, treat the connection as dead after missing a few pings
    const STALE_MS = 75000
    // Phonon (https://github.com/gradium-ai/xn-ptts, MIT or Apache-2.0) is the one outside library of the overlay:
    // a speech model that runs in this page. Unlike the browser's own speech, its sound is part of the page,
    // which is what OBS captures. The versions are pinned: these URLs always return the same files.
    // The address of the library comes from the page (phononLibrary), which allows just that address to run scripts.
    const PHONON_MODEL = 'https://huggingface.co/gradium/phonon-31m-en/resolve/92741c5b07d085ff840e26fea8ce43110b87852f/'
    const PHONON_VOICES = 'https://huggingface.co/gradium/phonon-voices/resolve/da6c9ec0d47be8df5c87c730a3aa97187f3229c3/voices/'

    const FAILED_STATUS_MS = 15000
    const stage = document.getElementById('CheddarChatAlert')
    const status = document.getElementById('CheddarChatStatus')
    const { socketUrl, image, defaultImage, voice, phononLibrary, phononWorker } = document.body.dataset
    const queue = []
    let playing = false
    let skipCurrent = null
    // Keep a reference, otherwise Chromium may garbage collect the utterance before it ends
    let utterance = null
    let socket = null
    let lastMessageAt = 0
    let retries = 0
    let clearTimer = null
    // The loaded Phonon model, once it is ready
    let phonon = null
    let phononLoading = null
    let audioContext = null

    // Warm the cache, so that the first alert does not wait for the image
    if (image || defaultImage) new Image().src = image || defaultImage

    function connect() {
        const url = new URL(socketUrl, window.location.href)
        url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'
        const ws = new WebSocket(url)
        socket = ws
        ws.addEventListener('open', () => {
            retries = 0
            lastMessageAt = Date.now()
        })
        ws.addEventListener('message', ({ data }) => {
            lastMessageAt = Date.now()
            if (data === 'ping') return
            let message
            try {
                message = JSON.parse(data)
            } catch {
                return
            }
            if (message.type === 'alert') {
                queue.push(message)
                if (!playing) playQueue()
            } else if (message.type === 'skip' && skipCurrent) {
                skipCurrent()
            }
        })
        ws.addEventListener('close', () => {
            if (socket === ws) reconnect()
        })
    }

    function reconnect() {
        socket = null
        setTimeout(connect, Math.min(30000, 1000 * 2 ** retries++))
    }

    setInterval(() => {
        if (socket && socket.readyState === WebSocket.OPEN && Date.now() - lastMessageAt > STALE_MS) {
            const stale = socket
            reconnect()
            stale.close()
        }
    }, 5000)

    async function playQueue() {
        playing = true
        while (queue.length) await play(queue.shift())
        playing = false
    }

    function play(alert) {
        return new Promise(resolve => {
            const timers = []
            let done = false
            let minDurationPassed = false
            const usePhonon = !!(alert.voice && phonon)
            // What the voice reads: the message, with links shortened to their site
            const speech = alert.speech || alert.message
            let speechEnded = !alert.tts || !speech || !(usePhonon || 'speechSynthesis' in window)
            let stopSpeech = null

            const finish = () => {
                if (done) return
                done = true
                skipCurrent = null
                timers.forEach(clearTimeout)
                if (stopSpeech) stopSpeech()
                if (utterance) {
                    utterance = null
                    window.speechSynthesis.cancel()
                }
                hide()
                setTimeout(resolve, FADE_MS)
            }
            const finishWhenReady = () => {
                if (minDurationPassed && speechEnded) finish()
            }

            skipCurrent = finish
            show(alert)

            timers.push(setTimeout(() => {
                minDurationPassed = true
                finishWhenReady()
            }, Math.max(1, alert.duration || 8) * 1000))
            timers.push(setTimeout(finish, MAX_ALERT_MS))
            if (!speechEnded) {
                const speechDone = () => {
                    if (speechEnded) return
                    speechEnded = true
                    finishWhenReady()
                }
                timers.push(setTimeout(() => {
                    if (usePhonon) stopSpeech = speakWithPhonon(speech, alert.voice, speechDone)
                    else speak(speech, speechDone)
                }, SPEAK_DELAY_MS))
                // Still downloading, or the voice got changed in the settings: be ready for the next alert
                if (alert.voice && !phonon) loadPhonon(alert.voice)
                // A stuck speech engine never fires "end": don't let it hold the alert and the queue until MAX_ALERT_MS.
                // finish() then cancels the stuck utterance.
                timers.push(setTimeout(speechDone, SPEAK_DELAY_MS + SPEECH_BASE_MS + speech.length * SPEECH_MS_PER_CHAR))
            }
        })
    }

    function speak(text, onEnd) {
        const current = new SpeechSynthesisUtterance(text)
        const ended = () => {
            if (utterance !== current) return
            utterance = null
            onEnd()
        }
        current.addEventListener('end', ended)
        current.addEventListener('error', ended)
        utterance = current
        window.speechSynthesis.speak(current)
    }

    // Downloads (once, the browser keeps the files) and starts the Phonon model
    function loadPhonon(voiceId) {
        if (!phononLoading) {
            const started = performance.now()
            // Bytes per file that really come from the network. Nothing is shown when the browser has the files already.
            const downloads = new Map()
            const onProgress = ({ file, loaded, total, cached }) => {
                if (cached) return
                downloads.set(file, { loaded, total: total || loaded })
                let done = 0
                let size = 0
                for (const download of downloads.values()) {
                    done += download.loaded
                    size += download.total
                }
                // The small files come first: wait for the model itself before showing a percentage
                if (size > 1024 * 1024) showStatus(`${status.dataset.downloading} ${Math.floor(done / size * 100)}%`, done / size)
            }
            phononLoading = import(`${phononLibrary}index.js`)
                .then(({ PhononTTS }) => PhononTTS.load({
                    lang: 'en',
                    workerUrl: phononWorker,
                    onProgress,
                    model: {
                        weights: { q8: PHONON_MODEL + 'model.q8.gguf' },
                        tokenizer: PHONON_MODEL + 'tokenizer.json',
                        config: PHONON_MODEL + 'config.json',
                        voices: { [voiceId]: phononVoiceUrl(voiceId) },
                        defaultVoice: voiceId
                    }
                }))
                .then(tts => {
                    if (downloads.size) showStatus(status.dataset.preparing)
                    // The first speech takes longer: get that over with before the first alert
                    return tts.synth('Ready.').then(() => { phonon = tts })
                })
                .then(() => {
                    hideStatus()
                    const megabytes = [...downloads.values()].reduce((sum, download) => sum + download.loaded, 0) / 1e6
                    console.info(`CheddarChat Lite: voice model ready after ${((performance.now() - started) / 1000).toFixed(1)} s` +
                        (downloads.size ? ` (downloaded ${megabytes.toFixed(0)} MB)` : ' (from the browser cache)'))
                })
                .catch(err => {
                    // The browser's own voice is used meanwhile, the next alert tries again
                    console.warn('Could not load the Phonon voice', err)
                    phononLoading = null
                    showStatus(status.dataset.failed)
                    setTimeout(hideStatus, FAILED_STATUS_MS)
                })
        }
        return phononLoading
    }

    // Shows what the voice model is doing, with a progress bar when a fraction (0 to 1) is given
    function showStatus(text, fraction) {
        status.firstElementChild.textContent = text
        const bar = status.lastElementChild
        bar.hidden = fraction === undefined
        bar.firstElementChild.style.width = `${Math.round((fraction || 0) * 100)}%`
        status.hidden = false
    }

    function hideStatus() {
        status.hidden = true
    }

    function phononVoiceUrl(voiceId) {
        return `${PHONON_VOICES}${encodeURIComponent(voiceId)}.safetensors`
    }

    // The browser keeps the downloaded model files for good. Remove those of earlier model versions, which
    // nothing uses anymore (about 70 MB each).
    async function removeOldPhononFiles() {
        if (!('caches' in window)) return
        for (const name of await caches.keys()) {
            if (!name.startsWith('phonon-tts')) continue
            const cache = await caches.open(name)
            for (const request of await cache.keys()) {
                if (!request.url.startsWith(PHONON_MODEL) && !request.url.startsWith(PHONON_VOICES)) await cache.delete(request)
            }
        }
    }

    // Plays the speech while it is still being generated. Returns a function that stops it.
    function speakWithPhonon(text, voiceId, onEnd) {
        const controller = new AbortController()
        const sources = new Set()
        let stopped = false
        const speaking = async () => {
            if (!phonon.voices.includes(voiceId)) await phonon.addVoice(voiceId, phononVoiceUrl(voiceId))
            if (!audioContext) audioContext = new AudioContext({ sampleRate: phonon.sampleRate })
            if (audioContext.state === 'suspended') await audioContext.resume()
            let at = 0
            for await (const pcm of phonon.stream(text, { voice: voiceId, signal: controller.signal })) {
                if (stopped) return
                const buffer = audioContext.createBuffer(1, pcm.length, phonon.sampleRate)
                buffer.getChannelData(0).set(pcm)
                const source = audioContext.createBufferSource()
                source.buffer = buffer
                source.connect(audioContext.destination)
                source.addEventListener('ended', () => sources.delete(source))
                sources.add(source)
                // Continue seamlessly, or from now if generating fell behind
                at = Math.max(at, audioContext.currentTime)
                source.start(at)
                at += buffer.duration
            }
            // Everything is generated, wait for the rest to play
            const remaining = Math.max(0, at - audioContext.currentTime)
            await new Promise(resolve => setTimeout(resolve, remaining * 1000))
        }
        speaking()
            .catch(err => {
                if (!stopped) console.warn('Phonon could not speak', err)
            })
            .then(() => {
                if (!stopped) onEnd()
            })
        return () => {
            stopped = true
            controller.abort()
            for (const source of sources) source.stop()
            sources.clear()
        }
    }

    function show(alert) {
        clearTimeout(clearTimer)
        render(alert)
        void stage.offsetWidth // restart the CSS transition
        stage.classList.add('is-visible')
    }

    function hide() {
        stage.classList.remove('is-visible')
        clearTimeout(clearTimer)
        clearTimer = setTimeout(() => stage.replaceChildren(), FADE_MS)
    }

    function render(alert) {
        const nodes = []
        // Without an image of their own, alerts show the Bitcoin logo of BTCPay Server, which bounces
        const src = alert.image || defaultImage
        if (src) {
            const img = new Image()
            img.className = alert.image ? 'cc-alert__image' : 'cc-alert__image cc-alert__image--default'
            img.alt = ''
            img.referrerPolicy = 'no-referrer'
            img.src = src
            nodes.push(img)
        }
        const title = element('div', 'cc-alert__title')
        title.append(element('span', 'cc-alert__name', alert.name), ' sent ', element('span', 'cc-alert__amount', alert.amount))
        nodes.push(title)
        if (alert.message) nodes.push(element('div', 'cc-alert__message', alert.message))
        stage.replaceChildren(...nodes)
    }

    function element(tagName, className, text) {
        const el = document.createElement(tagName)
        el.className = className
        if (text) el.textContent = text
        return el
    }

    removeOldPhononFiles().catch(err => console.warn('Could not remove old voice model files', err))
    if (voice) loadPhonon(voice)
    connect()
})()
