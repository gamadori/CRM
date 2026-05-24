import * as THREE from './three.module.js';
import { OrbitControls } from './OrbitControls.js';
import { DXFLoader } from './DXFLoader.js';

const viewers = new Map();

window.loadDxfFromBytes = (canvasId, bytes) => {
    console.log("DXF bytes ricevuti:", bytes.length);

    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error("Canvas non trovato:", canvasId);
        return;
    }

    cleanupViewer(canvasId);

    const blob = new Blob([bytes], { type: 'application/dxf' });
    const url = URL.createObjectURL(blob);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf0f0f0);

    const camera = new THREE.PerspectiveCamera(60, 1, 0.1, 10000);
    camera.position.set(0, 200, 400);

    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
    renderer.setSize(canvas.clientWidth, canvas.clientHeight);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;

    scene.add(new THREE.AmbientLight(0xffffff, 0.8));

    const state = {
        scene,
        camera,
        renderer,
        controls,
        animationFrameId: null,
        objectUrl: url
    };

    viewers.set(canvasId, state);

    const loader = new DXFLoader();
    loader.load(url, obj => {
        if (!viewers.has(canvasId)) {
            URL.revokeObjectURL(url);
            return;
        }

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

        animate(canvasId);
    }, undefined, error => {
        console.error("Errore caricamento DXF:", error);
        URL.revokeObjectURL(url);
    });
};

window.exportDxfImage = (canvasId, fileName) => {
    const canvas = document.getElementById(canvasId);
    const viewer = viewers.get(canvasId);

    if (!canvas || !viewer) {
        alert("Canvas non trovato");
        return;
    }

    viewer.scene.background = new THREE.Color(0xffffff);
    viewer.renderer.render(viewer.scene, viewer.camera);

    const link = document.createElement("a");
    link.href = canvas.toDataURL("image/png");
    link.download = fileName || "dxf.png";
    link.click();
};

window.exportDxfImageHighRes = (
    canvasId,
    fileName,
    scale = 3
) => {
    const canvas = document.getElementById(canvasId);
    const viewer = viewers.get(canvasId);

    if (!canvas || !viewer) {
        alert("Viewer non inizializzato");
        return;
    }

    const { scene, camera, renderer } = viewer;
    const prevSize = renderer.getSize(new THREE.Vector2());
    const prevPixelRatio = renderer.getPixelRatio();
    const prevBackground = scene.background;

    scene.background = new THREE.Color(0xffffff);

    renderer.setPixelRatio(prevPixelRatio * scale);
    renderer.setSize(prevSize.x, prevSize.y, false);
    renderer.render(scene, camera);

    const dataUrl = canvas.toDataURL("image/png");

    const link = document.createElement("a");
    link.href = dataUrl;
    link.download = fileName || "dxf_highres.png";
    link.click();

    renderer.setPixelRatio(prevPixelRatio);
    renderer.setSize(prevSize.x, prevSize.y, false);
    scene.background = prevBackground;
    renderer.render(scene, camera);
};

window.cleanupDxfViewer = canvasId => {
    cleanupViewer(canvasId);
};

function animate(canvasId) {
    const viewer = viewers.get(canvasId);
    if (!viewer) {
        return;
    }

    const { scene, camera, renderer, controls } = viewer;
    viewer.animationFrameId = requestAnimationFrame(() => animate(canvasId));
    controls.update();
    renderer.render(scene, camera);
}

function cleanupViewer(canvasId) {
    const viewer = viewers.get(canvasId);
    if (!viewer) {
        return;
    }

    if (viewer.animationFrameId) {
        cancelAnimationFrame(viewer.animationFrameId);
    }

    viewer.controls?.dispose();
    viewer.renderer?.dispose();

    if (viewer.objectUrl) {
        URL.revokeObjectURL(viewer.objectUrl);
    }

    viewers.delete(canvasId);
}
