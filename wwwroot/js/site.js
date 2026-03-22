document.addEventListener("DOMContentLoaded", () => {
    const startButton = document.getElementById("startRecordingButton");
    const stopButton = document.getElementById("stopRecordingButton");
    const loadButton = document.getElementById("loadRecordingButton");
    const submitButton = document.getElementById("submitAudioButton");
    const audioStatus = document.getElementById("audioRecordingStatus");
    const audioPreview = document.getElementById("audioPreview");
    const audioFileInput = document.getElementById("audioFile");

    if (!startButton || !stopButton || !loadButton || !submitButton || !audioStatus || !audioPreview || !audioFileInput) {
        return;
    }

    let mediaRecorder;
    let mediaStream;
    let audioChunks = [];

    startButton.addEventListener("click", async () => {
        try {
            mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaRecorder = new MediaRecorder(mediaStream);
            audioChunks = [];

            mediaRecorder.addEventListener("dataavailable", (event) => {
                if (event.data.size > 0) {
                    audioChunks.push(event.data);
                }
            });

            mediaRecorder.addEventListener("stop", () => {
                const audioBlob = new Blob(audioChunks, { type: "audio/webm" });
                const audioFile = new File([audioBlob], `gravacao-${Date.now()}.webm`, { type: "audio/webm" });
                const dataTransfer = new DataTransfer();

                dataTransfer.items.add(audioFile);
                audioFileInput.files = dataTransfer.files;
                audioPreview.src = URL.createObjectURL(audioBlob);
                audioPreview.hidden = false;
                submitButton.disabled = false;
                audioStatus.textContent = "Gravacao concluida. Agora voce pode enviar o audio.";

                if (mediaStream) {
                    mediaStream.getTracks().forEach((track) => track.stop());
                }
            });

            mediaRecorder.start();
            startButton.disabled = true;
            stopButton.disabled = false;
            submitButton.disabled = true;
            audioStatus.textContent = "Gravando audio...";
        } catch (error) {
            console.error(error);
            audioStatus.textContent = "Nao foi possivel acessar o microfone deste computador.";
        }
    });

    stopButton.addEventListener("click", () => {
        if (!mediaRecorder || mediaRecorder.state !== "recording") {
            return;
        }

        mediaRecorder.stop();
        startButton.disabled = false;
        stopButton.disabled = true;
    });

    loadButton.addEventListener("click", () => {
        audioFileInput.click();
    });

    audioFileInput.addEventListener("change", () => {
        const selectedFile = audioFileInput.files && audioFileInput.files.length > 0
            ? audioFileInput.files[0]
            : null;

        if (!selectedFile) {
            submitButton.disabled = true;
            audioPreview.hidden = true;
            audioStatus.textContent = "Nenhum arquivo de audio selecionado.";
            return;
        }

        audioPreview.src = URL.createObjectURL(selectedFile);
        audioPreview.hidden = false;
        submitButton.disabled = false;
        audioStatus.textContent = `Arquivo carregado: ${selectedFile.name}`;
        startButton.disabled = false;
        stopButton.disabled = true;
    });
});
