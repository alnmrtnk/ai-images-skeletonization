# AI Images Skeletonization

Проєкт для скелетонізації зображень з використанням алгоритмів комп'ютерного зору. Складається з фронтенду на Angular та бекенду на Python/FastAPI.

## 🚀 Особливості

- **Frontend**: Angular 18 з підтримкою інтернаціоналізації (українська/англійська)
- **Backend**: FastAPI з алгоритмами обробки зображень
- **Обробка зображень**: Скелетонізація з виділенням кінцевих та розгалужувальних точок
- **Автоматичний деплой**: GitHub Actions для CI/CD

## 📁 Структура проєкту

```
ai-images-skeletonization/
├── frontend/          # Angular додаток
│   ├── src/
│   ├── package.json
│   └── angular.json
├── backend/           # Python FastAPI сервер
│   ├── app.py
│   └── requirements.txt
├── .github/           # GitHub Actions workflows
├── .gitignore
└── README.md
```

## 🛠️ Встановлення та запуск

### Backend (Python)

1. Перейдіть до папки backend:
```bash
cd backend
```

2. Створіть віртуальне середовище:
```bash
python -m venv venv
```

3. Активуйте віртуальне середовище:
```bash
# Windows
venv\Scripts\activate
# macOS/Linux
source venv/bin/activate
```

4. Встановіть залежності:
```bash
pip install -r requirements.txt
```

5. Запустіть сервер:
```bash
uvicorn app:app --reload --host 0.0.0.0 --port 8000
```

### Frontend (Angular)

1. Перейдіть до папки frontend:
```bash
cd frontend
```

2. Встановіть залежності:
```bash
npm install
```

3. Запустіть додаток:
```bash
npm start
```

Додаток буде доступний за адресою: http://localhost:4200

## 🔧 API Endpoints

### POST /skeletonize
Обробляє зображення та повертає скелетонізований результат з виділеними точками.

**Параметри:**
- `file`: Файл зображення (multipart/form-data)

**Відповідь:**
- Оброблене зображення у форматі PNG з:
  - Червоними точками: кінцеві точки скелету
  - Синіми точками: точки розгалуження

## 🚀 Деплой

### Frontend на GitHub Pages

GitHub Actions автоматично деплоїть фронтенд на GitHub Pages при push до гілки `main`.

### Backend на Render/Railway

1. Створіть акаунт на [Render.com](https://render.com) або [Railway.app](https://railway.app)
2. Підключіть ваш GitHub репозиторій
3. Налаштуйте автоматичний деплой з папки `backend`

## 🔧 Налаштування розробки

### Локальна розробка

1. Запустіть backend на порту 8000
2. Запустіть frontend на порту 4200
3. Frontend автоматично проксує API запити до backend

### Продакшн

Для продакшну оновіть URL API в конфігурації Angular на реальний URL вашого backend.

## 📚 Технологічний стек

**Frontend:**
- Angular 18
- TypeScript
- SCSS
- ngx-translate (інтернаціоналізація)

**Backend:**
- Python 3.8+
- FastAPI
- OpenCV
- scikit-image
- NumPy

## 🤝 Внесок у проєкт

1. Зробіть fork репозиторію
2. Створіть гілку для вашої функції: `git checkout -b feature/amazing-feature`
3. Зробіть commit: `git commit -m 'Add amazing feature'`
4. Push до гілки: `git push origin feature/amazing-feature`
5. Відкрийте Pull Request

## 📝 Ліцензія

Цей проєкт ліцензовано під MIT License.

## 📞 Контакти

- GitHub: [@alnmrtnk](https://github.com/alnmrtnk)
- Проєкт: [ai-images-skeletonization](https://github.com/alnmrtnk/ai-images-skeletonization)