# VideoPlatform — الباك

الـAPI مسؤول عن التسجيل والدخول، مكتبة المستخدم، روابط الرفع، وإرسال مهام المعالجة. الـWorker بيستهلك المهام من RabbitMQ ويحوّل الفيديو إلى HLS بـFFmpeg. PostgreSQL بيحفظ البيانات، وMinIO أو S3 بيخزّن ملفات الفيديو.

## تشغيل محلي

المطلوب .NET 10 SDK وDocker Compose، ووجود `ffmpeg` و`ffprobe` على PATH لتشغيل الـWorker خارج Docker. نفّذ من مسار `back` داخل الحزمة:

```sh
docker compose up -d
```

شغّل الـAPI والـWorker في طرفيتين منفصلتين من نفس المسار:

```sh
dotnet run --project src/VideoPlatform.Api
dotnet run --project src/VideoPlatform.Worker
```

الـAPI على `http://localhost:5000`. استخدم `/api/health` لفحص الاستجابة و`/api/health/ready` لفحص الجاهزية. الواجهة المتصلة محليًا بتستخدم `http://localhost:5173`.

## الكود والاختبارات

- `back/src` فيه مشاريع Domain وApplication وInfrastructure وApi وWorker.
- `back/tests/VideoPlatform.Tests` فيه اختبارات xUnit. شغّل `dotnet test VideoPlatform.sln` من `back`.
- المشروع الحالي بيجهّز قاعدة جديدة من نموذج EF Core عند غياب migrations. خطوات الانتقال لـmigrations موجودة في [دليل النشر](DEPLOYMENT.md#7-قاعدة-البيانات-والـ-migrations).

## Docker والنشر

من جذر الحزمة، انسخ `.env.example` إلى `.env`، اضبط القيم المناسبة للخادم، وشغّل `docker compose up -d --build`. ملف Compose في جذر الحزمة بيشغّل الـAPI والـWorker والخدمات، وملف Compose داخل `back` مخصص لخدمات التطوير المحلي. إعدادات Caddy وHTTPS والتخزين والربط بالواجهة في [دليل النشر](DEPLOYMENT.md).
