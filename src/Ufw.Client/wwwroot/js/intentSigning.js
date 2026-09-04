const PRIVATE_KEY_BEGIN = "-----BEGIN PRIVATE KEY-----";
const PRIVATE_KEY_END = "-----END PRIVATE KEY-----";
const ENCRYPTED_PRIVATE_KEY_BEGIN = "-----BEGIN ENCRYPTED PRIVATE KEY-----";
const KEY_ID_PREFIX = "sha256:";

export function createNonce(size) {
    if (!Number.isInteger(size) || size <= 0) {
        throw new Error("Nonce size must be a positive integer.");
    }

    const bytes = new Uint8Array(size);
    crypto.getRandomValues(bytes);
    return toBase64Url(bytes);
}

export async function getKeyId(privateKeyText) {
    const privateKey = await importPrivateKey(privateKeyText, true);
    const privateJwk = await crypto.subtle.exportKey("jwk", privateKey);
    const publicJwk = {
        kty: privateJwk.kty,
        crv: privateJwk.crv,
        x: privateJwk.x,
        y: privateJwk.y,
        ext: true,
    };
    const publicKey = await crypto.subtle.importKey(
        "jwk",
        publicJwk,
        { name: "ECDSA", namedCurve: "P-256" },
        true,
        ["verify"]);
    const spki = await crypto.subtle.exportKey("spki", publicKey);
    const digest = await crypto.subtle.digest("SHA-256", spki);
    return KEY_ID_PREFIX + toBase64Url(new Uint8Array(digest));
}

export async function sign(privateKeyText, data) {
    const privateKey = await importPrivateKey(privateKeyText, false);
    const signature = await crypto.subtle.sign(
        { name: "ECDSA", hash: "SHA-256" },
        privateKey,
        data);
    return toBase64Url(new Uint8Array(signature));
}

async function importPrivateKey(privateKeyText, extractable) {
    const pkcs8 = decodePrivateKey(privateKeyText);
    try {
        return await crypto.subtle.importKey(
            "pkcs8",
            pkcs8,
            { name: "ECDSA", namedCurve: "P-256" },
            extractable,
            ["sign"]);
    } catch (error) {
        throw new Error("The private key must be an unencrypted PKCS#8 ECDSA P-256 key.", { cause: error });
    }
}

function decodePrivateKey(privateKeyText) {
    if (typeof privateKeyText !== "string" || privateKeyText.trim().length === 0) {
        throw new Error("A private key is required for this request.");
    }
    if (privateKeyText.includes(ENCRYPTED_PRIVATE_KEY_BEGIN)) {
        throw new Error("Encrypted private keys are not supported in this first iteration.");
    }

    const base64 = privateKeyText
        .replaceAll(PRIVATE_KEY_BEGIN, "")
        .replaceAll(PRIVATE_KEY_END, "")
        .replace(/\s+/g, "");
    if (base64.length === 0) {
        throw new Error("The private key is empty.");
    }

    let binary;
    try {
        binary = atob(base64);
    } catch (error) {
        throw new Error("The private key is not valid PKCS#8 PEM or base64 DER.", { cause: error });
    }

    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }
    return bytes.buffer;
}

function toBase64Url(bytes) {
    let binary = "";
    for (let index = 0; index < bytes.length; index++) {
        binary += String.fromCharCode(bytes[index]);
    }

    return btoa(binary)
        .replaceAll("+", "-")
        .replaceAll("/", "_")
        .replace(/=+$/g, "");
}
