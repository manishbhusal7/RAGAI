import { useState, useEffect, useCallback } from 'react';
import { FileRecord, FileState } from '../types';
import { apiService } from '../services/apiService';

export const useDocuments = () => {
  const [uploadedFiles, setUploadedFiles] = useState<FileRecord[]>([]);
  const [activeFileIds, setActiveFileIds] = useState<string[]>([]);

  // Load uploaded files on mount
  useEffect(() => {
    loadUploadedFiles();
  }, []);

  const loadUploadedFiles = useCallback(async () => {
    try {
      console.log('Loading uploaded files...');
      const files = await apiService.getUploadedFiles();
      console.log('Raw files from API:', files);
      
      const mappedFiles = files.map(file => ({
        fileId: file.fileId || file.fileName || file.id, // Use fileName as fileId if fileId not available
        fileName: file.fileName || file.name,
        fileSize: file.fileSize || file.size || 0,
        fileType: file.fileType || file.type || 'application/octet-stream',
        createdAt: new Date(file.createdAt || file.uploadedAt || Date.now()),
        fileState: FileState.COMPLETED, // All files from server are completed
        progressPercent: 100
      }));
      
      console.log('Mapped files:', mappedFiles);
      setUploadedFiles(mappedFiles);
      
      // Set all files as active by default since they're all completed
      const allFileIds = mappedFiles.map(file => file.fileId);
      setActiveFileIds(allFileIds);
      console.log('Active file IDs:', allFileIds);
    } catch (error) {
      console.error('Error loading uploaded files:', error);
    }
  }, []);

  const uploadFiles = useCallback(async (files: File[]): Promise<void> => {
    const uploadPromises = files.map(async (file) => {
      // Create temporary file record
      const tempFileRecord: FileRecord = {
        fileId: Date.now().toString() + Math.random().toString(),
        fileName: file.name,
        fileSize: file.size,
        fileType: file.type,
        createdAt: new Date(),
        fileState: FileState.INITIATING,
        progressPercent: 0
      };

      // Add temporary record to UI
      setUploadedFiles(prev => [...prev, tempFileRecord]);

      try {
        // Show analyzing state briefly
        setUploadedFiles(prev => prev.map(f => 
          f.fileId === tempFileRecord.fileId 
            ? { ...f, fileState: FileState.ANALYZING, progressPercent: 50 }
            : f
        ));

        // Upload file
        const uploadResult = await apiService.uploadFile(file);
        console.log('Upload successful:', uploadResult);
        
        // Backend processes files immediately, so mark as completed
        setUploadedFiles(prev => prev.map(f => 
          f.fileId === tempFileRecord.fileId 
            ? {
                ...f,
                fileId: uploadResult.fileId,
                fileName: uploadResult.fileName,
                fileState: FileState.COMPLETED,
                progressPercent: 100
              }
            : f
        ));

        // Add to active files immediately since upload is complete
        setActiveFileIds(prev => [...prev, uploadResult.fileId]);
        console.log('File marked as completed:', uploadResult.fileId);

      } catch (error) {
        console.error('Error uploading file:', error);
        setUploadedFiles(prev => prev.map(f => 
          f.fileId === tempFileRecord.fileId 
            ? { ...f, fileState: FileState.FAILED, progressPercent: 0 }
            : f
        ));
      }
    });

    await Promise.all(uploadPromises);
  }, []);

  const removeFile = useCallback(async (file: FileRecord): Promise<void> => {
    try {
      // Backend expects fileName for deletion, not fileId
      await apiService.deleteFile(file.fileName);
      setUploadedFiles(prev => prev.filter(f => f.fileId !== file.fileId));
      setActiveFileIds(prev => prev.filter(id => id !== file.fileId));
    } catch (error) {
      console.error('Error removing file:', error);
      throw error; // Re-throw to let UI handle error display
    }
  }, []);

  const toggleFileActive = useCallback((fileId: string) => {
    setActiveFileIds(prev => 
      prev.includes(fileId) 
        ? prev.filter(id => id !== fileId)
        : [...prev, fileId]
    );
  }, []);

  const clearAllFiles = useCallback(async () => {
    try {
      // Delete all files from server using fileName
      await Promise.all(uploadedFiles.map(file => apiService.deleteFile(file.fileName)));
      setUploadedFiles([]);
      setActiveFileIds([]);
    } catch (error) {
      console.error('Error clearing all files:', error);
      throw error; // Re-throw to let UI handle error display
    }
  }, [uploadedFiles]);

  return {
    uploadedFiles,
    activeFileIds,
    uploadFiles,
    removeFile,
    toggleFileActive,
    clearAllFiles,
    loadUploadedFiles
  };
}; 