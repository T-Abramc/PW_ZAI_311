process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0"; 

const args = process.argv.slice(2);
const getArg = (name, def) => {
    const idx = args.indexOf(name);
    return idx !== -1 && args[idx + 1] ? args[idx + 1] : def;
};

const config = {
    url: (getArg("--url", process.env.API_URL || "https://localhost:44343")).replace(/\/$/, ""),
    apiKey: getArg("--api-key", process.env.SENSOR_API_KEY || ""),
    mode: getArg("--mode", "history"),       
    waveform: getArg("--waveform", "sine"),  
    count: parseInt(getArg("--count", "15"), 10),
    interval: parseInt(getArg("--interval", "60"), 10), 
    min: parseFloat(getArg("--min", "15")),
    max: parseFloat(getArg("--max", "25")),
    seedF11: args.includes("--seed-f11"),
    user: getArg("--user", "admin"),
    pass: getArg("--pass", "haslo")
};

function generateValue(waveform, step, min, max) {
    if (waveform === "sine") {
        const amplitude = (max - min) / 2.0;
        const mid = min + amplitude;
        return Number((mid + amplitude * Math.sin(step * 0.45)).toFixed(2));
    }
    return Number((min + Math.random() * (max - min)).toFixed(2));
}

async function sendMeasurement(url, apiKey, value, timestampIso) {
    const res = await fetch(`${url}/api/measurements`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-API-Key": apiKey
        },
        body: JSON.stringify({ value, timestamp: timestampIso })
    });

    if (res.status === 201) {
        console.log(`[OK 201] Wysłano pomiar: wartość = ${value} | czas = ${timestampIso}`);
    } else if (res.status === 422 || res.status === 400) {
        const problem = await res.json().catch(() => ({}));
        console.error(`[ODRZUCONO POMIAR - HTTP ${res.status}] Serwer odrzucił wartość ${value}! Powód: ${problem.detail || problem.title || "Wartość spoza przedziału min/max serii"}`);
    } else if (res.status === 401) {
        console.error(`[BŁĄD AUTORYZACJI - HTTP 401] Odmowa dostępu: klucz API czujnika jest niepoprawny lub czujnik został wyrejestrowany.`);
    } else {
        console.error(`[BŁĄD HTTP ${res.status}] ${await res.text()}`);
    }
}

async function runSeedF11() {
    console.log(`=== Automatyczne przygotowanie danych przykładowych F11 na ${config.url} ===`);
    const loginRes = await fetch(`${config.url}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: config.user, password: config.pass })
    });

    if (!loginRes.ok) {
        console.error(`Błąd logowania administratora (${loginRes.status}). Sprawdź --user oraz --pass.`);
        process.exit(1);
    }

    const { accessToken } = await loginRes.json();
    const authHeaders = {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${accessToken}`
    };

    const seriesDefs = [
        { name: "Temperatura Zewnętrzna", minValue: -20, maxValue: 45, color: "#e63946", icon: "thermometer", unit: "°C", gMin: 14, gMax: 24, wave: "sine" },
        { name: "Wilgotność Powietrza", minValue: 0, maxValue: 100, color: "#457b9d", icon: "droplet", unit: "%", gMin: 45, gMax: 75, wave: "sine" },
        { name: "Stężenie Pyłu PM2.5", minValue: 0, maxValue: 500, color: "#2a9d8f", icon: "wind", unit: "µg/m³", gMin: 12, gMax: 55, wave: "random" }
    ];

    for (const s of seriesDefs) {
        const sRes = await fetch(`${config.url}/api/series`, {
            method: "POST",
            headers: authHeaders,
            body: JSON.stringify({ name: s.name, minValue: s.minValue, maxValue: s.maxValue, color: s.color, icon: s.icon, unit: s.unit })
        });
        const createdSeries = await sRes.json();

        const senRes = await fetch(`${config.url}/api/sensors`, {
            method: "POST",
            headers: authHeaders,
            body: JSON.stringify({ name: `Czujnik - ${s.name}`, seriesId: createdSeries.id })
        });
        const createdSensor = await senRes.json();

        console.log(`\nUtworzono serię "${s.name}" (ID: ${createdSeries.id}) i czujnik. Wysyłanie 18 pomiarów...`);
        const startMs = Date.now() - 18 * 900 * 1000;
        for (let i = 0; i < 18; i++) {
            const val = generateValue(s.wave, i, s.gMin, s.gMax);
            const ts = new Date(startMs + i * 900 * 1000).toISOString();
            await sendMeasurement(config.url, createdSensor.apiKey, val, ts);
        }
    }
    console.log("\n=== Zakończono generowanie danych przykładowych F11 ===");
}

async function main() {
    if (config.seedF11) {
        await runSeedF11();
        return;
    }

    if (!config.apiKey) {
        console.log(`Użycie generatora (F13):
  1. Wysłanie serii wyników z przeszłości (history):
     node generator.mjs --url https://localhost:44343 --api-key KLUCZ_API --mode history --count 15 --interval 60 --waveform sine --min 15 --max 25

  2. Wysyłanie bieżących wyników w stałych odstępach czasu (live):
     node generator.mjs --url https://localhost:44343 --api-key KLUCZ_API --mode live --interval 5 --waveform random --min 18 --max 22

  3. Test odrzucenia wyniku spoza zakresu (F4 - zwraca czytelny błąd 422):
     node generator.mjs --url https://localhost:44343 --api-key KLUCZ_API --mode history --count 1 --min 999 --max 1000

  4. Automatyczne utworzenie 3 serii po 18 punktów (F11):
     node generator.mjs --url https://localhost:44343 --seed-f11 --user admin --pass haslo`);
        process.exit(1);
    }

    if (config.mode === "history") {
        console.log(`[TRYB HISTORYCZNY] Wysyłanie ${config.count} wyników do ${config.url}...`);
        const startMs = Date.now() - config.count * config.interval * 1000;
        for (let i = 0; i < config.count; i++) {
            const val = generateValue(config.waveform, i, config.min, config.max);
            const ts = new Date(startMs + i * config.interval * 1000).toISOString();
            await sendMeasurement(config.url, config.apiKey, val, ts);
        }
    } else {
        console.log(`[TRYB LIVE] Wysyłanie pomiarów co ${config.interval}s do ${config.url} (Ctrl+C aby zakończyć)...`);
        let step = 0;
        while (true) {
            const val = generateValue(config.waveform, step++, config.min, config.max);
            await sendMeasurement(config.url, config.apiKey, val, new Date().toISOString());
            await new Promise(r => setTimeout(r, config.interval * 1000));
        }
    }
}

main();