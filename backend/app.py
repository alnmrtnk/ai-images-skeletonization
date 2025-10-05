from fastapi import FastAPI, File, UploadFile
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
import cv2
import numpy as np
from skimage.morphology import skeletonize
import io
import os

app = FastAPI(
    title="AI Images Skeletonization API",
    description="API для скелетонізації зображень з виділенням кінцевих та розгалужувальних точок",
    version="1.0.0"
)

# CORS configuration
allowed_origins = [
    "http://localhost:4200",
    "https://alnmrtnk.github.io",
    "https://ai-images-skeletonization.vercel.app",  # Якщо використовуєш Vercel
]

# In production, you might want to add your actual frontend domain
if os.getenv("ENVIRONMENT") == "production":
    allowed_origins.append("https://your-production-domain.com")

app.add_middleware(
    CORSMiddleware,
    allow_origins=allowed_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "message": "AI Images Skeletonization API is running"}

@app.post("/skeletonize")
async def skeletonize_image(file: UploadFile = File(...)):
    contents = await file.read()
    npimg = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(npimg, cv2.IMREAD_GRAYSCALE)

    _, binary = cv2.threshold(img, 127, 255, cv2.THRESH_BINARY_INV)
    
    skeleton = skeletonize(binary // 255).astype(np.uint8) * 255
    
    points_img = cv2.cvtColor(skeleton, cv2.COLOR_GRAY2BGR)
    kernel = np.ones((3,3), np.uint8)

    for y in range(1, skeleton.shape[0]-1):
        for x in range(1, skeleton.shape[1]-1):
            if skeleton[y, x] == 255:
                neighborhood = skeleton[y-1:y+2, x-1:x+2]
                count = np.sum(neighborhood) // 255 - 1
                if count == 1:
                    cv2.circle(points_img, (x,y), 2, (0,0,255), -1)
                elif count > 2:
                    cv2.circle(points_img, (x,y), 2, (255,0,0), -1)

    _, buffer = cv2.imencode('.png', points_img)
    img_bytes = io.BytesIO(buffer.tobytes())
    
    return StreamingResponse(img_bytes, media_type="image/png")
