// Copyright � 2010-2014 The CefSharp Project. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.
#pragma once

#include "Stdafx.h"
#include "JavascriptObjectWrapper.h"
#include "JavascriptMethodHandler.h"

using namespace CefSharp::Internals;
using namespace System::Runtime::Serialization;

namespace CefSharp
{
    [DataContract]
    private ref class JavascriptMethodWrapper : IBindableJavascriptMember
    {
    private:
        MCefRefPtr<JavascriptMethodHandler> _javascriptMethodHandler;
        JavascriptObjectWrapper^ _owner;
        JavascriptMethod^ _method;

    public:
        JavascriptMethodWrapper(JavascriptMethod^ method)
        {
            _method = method;
            _javascriptMethodHandler = new JavascriptMethodHandler(gcnew Func<array<Object^>^, Object^>(this, &JavascriptMethodWrapper::Execute));
        }

        virtual void Bind(JavascriptObject^ owner)
        {
            _owner = static_cast<JavascriptObjectWrapper^>(owner);

            auto methodName = StringUtils::ToNative(_method->JavascriptName);
            auto v8Value = CefV8Value::CreateFunction(methodName, _javascriptMethodHandler.get());

            _owner->V8Value->SetValue(methodName, v8Value, V8_PROPERTY_ATTRIBUTE_NONE);
        };

        Object^ Execute(array<Object^>^ parameters);
    };
}