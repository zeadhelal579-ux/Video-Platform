# VideoPlatform — الواجهة المتصلة

واجهة React وTypeScript وVite للباك الحقيقي. المستخدم يقدر ينشئ حساب، يبحث في فيديوهاته، يرفع فيديو ويتابع حالة المعالجة، ويشغّل HLS عبر Video.js. تقدم المشاهدة بيتحفظ محليًا في المتصفح.

## التشغيل المحلي

شغّل الـAPI على `http://localhost:5000` والـWorker وخدماته. من فولدر الواجهة شغّل:

```sh
npm ci
npm run dev
```

افتح `http://localhost:5173`. لو عنوان الـAPI مختلف، انسخ `.env.example` إلى `.env.local` وعدّل `VITE_API_BASE_URL`. اضبط عنوان الواجهة في إعداد CORS الخاص بالباك.

## البناء والنشر

```sh
npm run typecheck
npm run build
```

ملفات الإنتاج في `dist`. افتراضيًا نسخة الإنتاج بتطلب `/api` على نفس الـorigin. استخدم reverse proxy للـAPI أو مرّر `VITE_API_BASE_URL` وقت البناء لو الـAPI على عنوان منفصل. إعدادات `Dockerfile` و`nginx.conf` بتخدم الواجهة وتوجّه طلبات `/api` إلى خدمة الباك المسماة `api` على شبكة الحاويات.
