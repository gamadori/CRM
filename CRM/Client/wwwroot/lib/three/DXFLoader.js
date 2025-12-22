import * as THREE from './three.module.js';

export class DXFLoader {

    load(url, onLoad) {
        fetch(url)
            .then(r => r.text())
            .then(text => {

                const lines = text.split(/\r?\n/);
                const group = new THREE.Group();

                let i = 0;

                // materiali per layer
                const materials = {};
                const getMaterial = (layer) => {
                    if (!materials[layer]) {
                        materials[layer] =
                            new THREE.LineBasicMaterial({ color: 0x000000 });
                    }
                    return materials[layer];
                };

                // helper sicuro per leggere coppie codice/valore
                const readPair = () => {
                    if (i + 1 >= lines.length) return null;
                    const code = lines[i]?.trim();
                    const value = lines[i + 1]?.trim();
                    i += 2;
                    return { code, value };
                };

                while (i + 1 < lines.length) {

                    const pair = readPair();
                    if (!pair) break;

                    if (pair.code !== '0') continue;

                    const type = pair.value;

                    // ================= LINE =================
                    if (type === 'LINE') {
                        let x1, y1, x2, y2, layer = '0';

                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p || p.code === '0') {
                                if (p && p.code === '0') i -= 2;
                                break;
                            }

                            if (p.code === '8') layer = p.value;
                            if (p.code === '10') x1 = parseFloat(p.value);
                            if (p.code === '20') y1 = parseFloat(p.value);
                            if (p.code === '11') x2 = parseFloat(p.value);
                            if (p.code === '21') y2 = parseFloat(p.value);
                        }

                        if (x1 != null && y1 != null && x2 != null && y2 != null) {
                            const geo = new THREE.BufferGeometry().setFromPoints([
                                new THREE.Vector3(x1, y1, 0),
                                new THREE.Vector3(x2, y2, 0)
                            ]);
                            group.add(new THREE.Line(geo, getMaterial(layer)));
                        }
                    }

                    // ================= LWPOLYLINE =================
                    if (type === 'LWPOLYLINE') {
                        let layer = '0';
                        const points = [];
                        let x = null;

                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p || p.code === '0') {
                                if (p && p.code === '0') i -= 2;
                                break;
                            }

                            if (p.code === '8') layer = p.value;
                            if (p.code === '10') x = parseFloat(p.value);
                            if (p.code === '20' && x != null) {
                                const y = parseFloat(p.value);
                                points.push(new THREE.Vector3(x, y, 0));
                                x = null;
                            }
                        }

                        if (points.length > 1) {
                            const geo = new THREE.BufferGeometry().setFromPoints(points);
                            group.add(new THREE.Line(geo, getMaterial(layer)));
                        }
                    }

                    // ================= POLYLINE =================
                    if (type === 'POLYLINE') {
                        let layer = '0';
                        const points = [];

                        // header POLYLINE
                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p) break;

                            if (p.code === '8') layer = p.value;

                            if (p.code === '0' &&
                                (p.value === 'VERTEX' || p.value === 'SEQEND')) {
                                i -= 2;
                                break;
                            }
                        }

                        // VERTEX loop
                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p) break;

                            if (p.code === '0' && p.value === 'SEQEND') {
                                break;
                            }

                            if (p.code === '0' && p.value === 'VERTEX') {
                                let vx = null, vy = null;

                                while (i + 1 < lines.length) {
                                    const vp = readPair();
                                    if (!vp || vp.code === '0') {
                                        if (vp && vp.code === '0') i -= 2;
                                        break;
                                    }

                                    if (vp.code === '10') vx = parseFloat(vp.value);
                                    if (vp.code === '20') vy = parseFloat(vp.value);
                                }

                                if (vx != null && vy != null) {
                                    points.push(new THREE.Vector3(vx, vy, 0));
                                }
                            }
                        }

                        if (points.length > 1) {
                            const geo = new THREE.BufferGeometry().setFromPoints(points);
                            group.add(new THREE.Line(geo, getMaterial(layer)));
                        }
                    }

                    // ================= CIRCLE =================
                    if (type === 'CIRCLE') {
                        let cx, cy, r, layer = '0';

                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p || p.code === '0') {
                                if (p && p.code === '0') i -= 2;
                                break;
                            }

                            if (p.code === '8') layer = p.value;
                            if (p.code === '10') cx = parseFloat(p.value);
                            if (p.code === '20') cy = parseFloat(p.value);
                            if (p.code === '40') r = parseFloat(p.value);
                        }

                        if (cx != null && cy != null && r != null) {
                            const curve = new THREE.EllipseCurve(
                                cx, cy, r, r, 0, Math.PI * 2
                            );
                            const pts = curve.getPoints(64);
                            const geo = new THREE.BufferGeometry().setFromPoints(
                                pts.map(p => new THREE.Vector3(p.x, p.y, 0))
                            );
                            group.add(new THREE.Line(geo, getMaterial(layer)));
                        }
                    }

                    // ================= ARC =================
                    if (type === 'ARC') {
                        let cx, cy, r, a1, a2, layer = '0';

                        while (i + 1 < lines.length) {
                            const p = readPair();
                            if (!p || p.code === '0') {
                                if (p && p.code === '0') i -= 2;
                                break;
                            }

                            if (p.code === '8') layer = p.value;
                            if (p.code === '10') cx = parseFloat(p.value);
                            if (p.code === '20') cy = parseFloat(p.value);
                            if (p.code === '40') r = parseFloat(p.value);
                            if (p.code === '50') a1 = THREE.MathUtils.degToRad(parseFloat(p.value));
                            if (p.code === '51') a2 = THREE.MathUtils.degToRad(parseFloat(p.value));
                        }

                        if (cx != null && cy != null && r != null && a1 != null && a2 != null) {
                            const curve = new THREE.EllipseCurve(
                                cx, cy, r, r, a1, a2
                            );
                            const pts = curve.getPoints(32);
                            const geo = new THREE.BufferGeometry().setFromPoints(
                                pts.map(p => new THREE.Vector3(p.x, p.y, 0))
                            );
                            group.add(new THREE.Line(geo, getMaterial(layer)));
                        }
                    }
                }

                onLoad(group);
            });
    }
}
