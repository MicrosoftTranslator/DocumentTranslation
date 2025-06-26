class DocumentTranslationApp {
    constructor() {
        this.languages = [];
        this.supportedFormats = [];
        this.init();
    }

    async init() {
        try {
            await this.loadLanguages();
            await this.loadSupportedFormats();
            this.setupEventListeners();
            this.hideLoadingModal();
        } catch (error) {
            console.error('Failed to initialize app:', error);
            this.showError('Failed to initialize the application. Please refresh the page.');
        }
    }

    async loadLanguages() {
        try {
            const response = await fetch('/api/translation/languages');
            if (!response.ok) {
                throw new Error('Failed to load languages');
            }
            this.languages = await response.json();
            this.populateLanguageSelects();
        } catch (error) {
            console.error('Error loading languages:', error);
            throw error;
        }
    }

    async loadSupportedFormats() {
        try {
            const response = await fetch('/api/translation/formats');
            if (!response.ok) {
                throw new Error('Failed to load supported formats');
            }
            this.supportedFormats = await response.json();
        } catch (error) {
            console.error('Error loading supported formats:', error);
            // Don't throw here as this is not critical for basic functionality
        }
    }

    populateLanguageSelects() {
        const selects = [
            'textFromLanguage', 'textToLanguage',
            'docFromLanguage', 'docToLanguage',
            'batchFromLanguage', 'batchToLanguage'
        ];

        selects.forEach(selectId => {
            const select = document.getElementById(selectId);
            if (select) {
                // Clear existing options except the first one
                while (select.children.length > 1) {
                    select.removeChild(select.lastChild);
                }

                // Add language options
                this.languages.forEach(lang => {
                    const option = document.createElement('option');
                    option.value = lang.langCode;
                    option.textContent = `${lang.name} (${lang.langCode})`;
                    select.appendChild(option);
                });
            }
        });
    }

    setupEventListeners() {
        // Text translation form
        document.getElementById('textTranslationForm')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleTextTranslation();
        });

        // Document translation form
        document.getElementById('documentTranslationForm')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleDocumentTranslation();
        });

        // Batch translation form
        document.getElementById('batchTranslationForm')?.addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleBatchTranslation();
        });

        // File input validation
        document.getElementById('documentFile')?.addEventListener('change', (e) => {
            this.validateFileSize(e.target);
        });

        document.getElementById('batchFiles')?.addEventListener('change', (e) => {
            this.validateFileSize(e.target);
        });
    }

    validateFileSize(input) {
        const maxSize = 50 * 1024 * 1024; // 50MB
        const files = input.files;
        
        for (let file of files) {
            if (file.size > maxSize) {
                this.showError(`File "${file.name}" is too large. Maximum file size is 50MB.`);
                input.value = '';
                return false;
            }
        }
        return true;
    }

    async handleTextTranslation() {
        const formData = {
            text: document.getElementById('textToTranslate').value,
            fromLanguage: document.getElementById('textFromLanguage').value,
            toLanguage: document.getElementById('textToLanguage').value,
            category: document.getElementById('textCategory').value || null
        };

        if (!formData.text.trim()) {
            this.showError('Please enter text to translate.');
            return;
        }

        if (!formData.fromLanguage || !formData.toLanguage) {
            this.showError('Please select both source and target languages.');
            return;
        }

        try {
            this.showLoadingModal('Translating text...');
            
            const response = await fetch('/api/translation/text', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(formData)
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Translation failed');
            }

            const result = await response.json();
            this.displayTextTranslationResult(result);
            
        } catch (error) {
            console.error('Text translation error:', error);
            this.showError(`Translation failed: ${error.message}`);
        } finally {
            this.hideLoadingModal();
        }
    }

    async handleDocumentTranslation() {
        const fileInput = document.getElementById('documentFile');
        const file = fileInput.files[0];
        
        if (!file) {
            this.showError('Please select a document to translate.');
            return;
        }

        const formData = new FormData();
        formData.append('file', file);
        formData.append('fromLanguage', document.getElementById('docFromLanguage').value);
        formData.append('toLanguage', document.getElementById('docToLanguage').value);
        
        const category = document.getElementById('docCategory').value;
        if (category) {
            formData.append('category', category);
        }

        if (!formData.get('fromLanguage') || !formData.get('toLanguage')) {
            this.showError('Please select both source and target languages.');
            return;
        }

        try {
            this.showLoadingModal('Translating document...');
            
            const response = await fetch('/api/translation/document', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Document translation failed');
            }

            const result = await response.json();
            this.displayDocumentTranslationResult(result);
            
        } catch (error) {
            console.error('Document translation error:', error);
            this.showError(`Document translation failed: ${error.message}`);
        } finally {
            this.hideLoadingModal();
        }
    }

    async handleBatchTranslation() {
        const fileInput = document.getElementById('batchFiles');
        const files = fileInput.files;
        
        if (!files || files.length === 0) {
            this.showError('Please select documents to translate.');
            return;
        }

        const formData = new FormData();
        for (let file of files) {
            formData.append('files', file);
        }
        formData.append('fromLanguage', document.getElementById('batchFromLanguage').value);
        formData.append('toLanguage', document.getElementById('batchToLanguage').value);
        
        const category = document.getElementById('batchCategory').value;
        if (category) {
            formData.append('category', category);
        }

        if (!formData.get('fromLanguage') || !formData.get('toLanguage')) {
            this.showError('Please select both source and target languages.');
            return;
        }

        try {
            this.showLoadingModal('Starting batch translation...');
            
            const response = await fetch('/api/translation/batch', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Batch translation failed');
            }

            const result = await response.json();
            this.displayBatchTranslationResult(result);
            this.startStatusPolling(result.operationId);
            
        } catch (error) {
            console.error('Batch translation error:', error);
            this.showError(`Batch translation failed: ${error.message}`);
        } finally {
            this.hideLoadingModal();
        }
    }

    displayTextTranslationResult(result) {
        const resultDiv = document.getElementById('textTranslationResult');
        const translatedTextDiv = document.getElementById('translatedText');
        
        translatedTextDiv.innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Original Text (${result.fromLanguage}):</h6>
                    <p class="text-muted">${document.getElementById('textToTranslate').value}</p>
                </div>
                <div class="col-md-6">
                    <h6>Translated Text (${result.toLanguage}):</h6>
                    <p class="fw-bold">${result.translatedText}</p>
                </div>
            </div>
        `;
        
        resultDiv.style.display = 'block';
        resultDiv.scrollIntoView({ behavior: 'smooth' });
    }

    displayDocumentTranslationResult(result) {
        const resultDiv = document.getElementById('documentTranslationResult');
        const documentResultDiv = document.getElementById('documentResult');
        
        documentResultDiv.innerHTML = `
            <div class="alert alert-success">
                <h6><i class="fas fa-check-circle me-2"></i>Document Translation Completed</h6>
                <p class="mb-2"><strong>Original File:</strong> ${result.originalFileName}</p>
                <p class="mb-2"><strong>Languages:</strong> ${result.fromLanguage} → ${result.toLanguage}</p>
                <a href="${result.translatedDocumentUrl}" class="btn btn-success btn-sm" download>
                    <i class="fas fa-download me-2"></i>Download Translated Document
                </a>
            </div>
        `;
        
        resultDiv.style.display = 'block';
        resultDiv.scrollIntoView({ behavior: 'smooth' });
    }

    displayBatchTranslationResult(result) {
        const resultDiv = document.getElementById('batchTranslationResult');
        const batchResultDiv = document.getElementById('batchResult');
        
        batchResultDiv.innerHTML = `
            <div class="alert alert-info">
                <h6><i class="fas fa-clock me-2"></i>Batch Translation Started</h6>
                <p class="mb-2"><strong>Operation ID:</strong> ${result.operationId}</p>
                <p class="mb-2"><strong>Files:</strong> ${result.fileCount} documents</p>
                <p class="mb-0"><strong>Status:</strong> <span class="status-badge status-processing">${result.status}</span></p>
            </div>
        `;
        
        resultDiv.style.display = 'block';
        document.getElementById('batchProgress').style.display = 'block';
        resultDiv.scrollIntoView({ behavior: 'smooth' });
    }

    async startStatusPolling(operationId) {
        const pollInterval = 5000; // 5 seconds
        const maxAttempts = 120; // 10 minutes
        let attempts = 0;

        const poll = async () => {
            try {
                const response = await fetch(`/api/translation/status/${operationId}`);
                if (!response.ok) {
                    throw new Error('Failed to get status');
                }

                const status = await response.json();
                this.updateBatchProgress(status);

                if (status.status === 'Completed' || status.status === 'Failed') {
                    return; // Stop polling
                }

                attempts++;
                if (attempts < maxAttempts) {
                    setTimeout(poll, pollInterval);
                } else {
                    this.showError('Status polling timeout. Please check the status manually.');
                }
            } catch (error) {
                console.error('Status polling error:', error);
                this.showError('Failed to get translation status.');
            }
        };

        poll();
    }

    updateBatchProgress(status) {
        const progressBar = document.querySelector('#batchProgress .progress-bar');
        const batchResultDiv = document.getElementById('batchResult');
        
        if (progressBar) {
            progressBar.style.width = `${status.progress}%`;
            progressBar.setAttribute('aria-valuenow', status.progress);
        }

        const statusBadgeClass = status.status === 'Completed' ? 'status-completed' : 
                                status.status === 'Failed' ? 'status-failed' : 'status-processing';

        batchResultDiv.innerHTML = `
            <div class="alert alert-info">
                <h6><i class="fas fa-clock me-2"></i>Batch Translation Progress</h6>
                <p class="mb-2"><strong>Operation ID:</strong> ${status.operationId}</p>
                <p class="mb-2"><strong>Status:</strong> <span class="status-badge ${statusBadgeClass}">${status.status}</span></p>
                <p class="mb-2"><strong>Progress:</strong> ${status.progress}%</p>
                <p class="mb-2"><strong>Completed:</strong> ${status.completedDocuments}/${status.totalDocuments}</p>
                ${status.failedDocuments > 0 ? `<p class="mb-0 text-danger"><strong>Failed:</strong> ${status.failedDocuments}</p>` : ''}
            </div>
        `;
    }

    showLoadingModal(message = 'Processing...') {
        document.getElementById('loadingMessage').textContent = message;
        const modal = new bootstrap.Modal(document.getElementById('loadingModal'));
        modal.show();
    }

    hideLoadingModal() {
        const modal = bootstrap.Modal.getInstance(document.getElementById('loadingModal'));
        if (modal) {
            modal.hide();
        }
    }

    showError(message) {
        document.getElementById('errorMessage').textContent = message;
        const modal = new bootstrap.Modal(document.getElementById('errorModal'));
        modal.show();
    }
}

// Initialize the app when the DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    new DocumentTranslationApp();
});
