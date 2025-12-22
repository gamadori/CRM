import * as THREE from './three.module.js';
import { OrbitControls } from './OrbitControls.js';
import { DXFLoader } from './DXFLoader.js';

let scene, camera, renderer, controls;

window.loadDxfFromBytes = (canvasId, bytes) => {

    console.log("DXF bytes ricevuti:", bytes.length);
    const canvas = document.getElementById(canvasId);

    // Blob DXF
    const blob = new Blob([bytes], { type: 'application/dxf' });
    const url = URL.createObjectURL(blob);

    scene = new THREE.Scene();
    
    scene.background = new THREE.Color(0xf0f0f0);
   

    camera = new THREE.PerspectiveCamera(60, 1, 0.1, 10000);
    camera.position.set(0, 200, 400);

    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
    window.scene = scene;
    window.camera = camera;
    window.renderer = renderer;

    renderer.setSize(canvas.clientWidth, canvas.clientHeight);

    controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;

    scene.add(new THREE.AmbientLight(0xffffff, 0.8));

    const loader = new DXFLoader();
    loader.load(url, obj => {

        scene.add(obj);

        const box = new THREE.Box3().setFromObject(obj);
        const size = box.getSize(new THREE.Vector3());
        const center = box.getCenter(new THREE.Vector3());

        const maxDim = Math.max(size.x, size.y, size.z);
        const dist = maxDim * 1.5 || 1000;

        camera.position.set(center.x, center.y, dist);
        camera.lookAt(center);

        controls.target.copy(center);
        controls.update();

        animate();
    });
};

window.exportDxfImage = (canvasId, fileName) => {
    const canvas = document.getElementById(canvasId);

    if (!canvas) {
        alert("Canvas non trovato");
        return;
    }
    scene.background = new THREE.Color(0xffffff);

    const link = document.createElement("a");
    link.href = canvas.toDataURL("image/png");
    link.download = fileName || "dxf.png";
    link.click();
};

window.exportDxfImageHighRes = (
    canvasId,
    fileName,
    scale = 3 // fattore di scala (2–4 consigliato)
) => {

    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.renderer || !window.scene || !window.camera) {
        alert("Viewer non inizializzato");
        return;
    }

    // 🔹 Salva stato corrente
    const prevSize = renderer.getSize(new THREE.Vector2());
    const prevPixelRatio = renderer.getPixelRatio();
    const prevBackground = scene.background;

    // 🔹 Sfondo bianco tipo CAD (opzionale)
    scene.background = new THREE.Color(0xffffff);

    // 🔹 Aumenta risoluzione
    renderer.setPixelRatio(prevPixelRatio * scale);
    renderer.setSize(prevSize.x, prevSize.y, false);

    // 🔹 Render forzato
    renderer.render(scene, camera);

    // 🔹 Esporta PNG
    const dataUrl = canvas.toDataURL("image/png");

    const link = document.createElement("a");
    link.href = dataUrl;
    link.download = fileName || "dxf_highres.png";
    link.click();

    // 🔹 Ripristina stato
    renderer.setPixelRatio(prevPixelRatio);
    renderer.setSize(prevSize.x, prevSize.y, false);
    scene.background = prevBackground;

    renderer.render(scene, camera);
};


function animate() {
    requestAnimationFrame(animate);
    controls.update();
    renderer.render(scene, camera);
}
