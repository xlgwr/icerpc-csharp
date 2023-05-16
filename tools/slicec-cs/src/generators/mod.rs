// Copyright (c) ZeroC, Inc.

mod class_generator;
mod dispatch_generator;
mod enum_generator;
mod exception_generator;
mod generated_code;
mod namespace_generator;
mod proxy_generator;
mod struct_generator;

use generated_code::GeneratedCode;
use slice::code_block::CodeBlock;
use slice::grammar::{
    Class, CustomType, Enum, Enumerator, Exception, Field, Interface, Module, Operation, Parameter, Struct, TypeAlias,
};
use slice::slice_file::SliceFile;
use slice::visitor::Visitor;

struct Generator<'a> {
    pub generated_code: &'a mut GeneratedCode,
}

impl Visitor for Generator<'_> {
    fn visit_file(&mut self, _: &SliceFile) {}

    fn visit_module(&mut self, _: &Module) {}

    fn visit_struct(&mut self, struct_def: &Struct) {
        struct_generator::generate_struct(struct_def, self.generated_code);
    }

    fn visit_class(&mut self, class_def: &Class) {
        class_generator::generate_class(class_def, self.generated_code);
    }

    fn visit_exception(&mut self, exception_def: &Exception) {
        exception_generator::generate_exception(exception_def, self.generated_code);
    }

    fn visit_interface(&mut self, interface_def: &Interface) {
        proxy_generator::generate_proxy(interface_def, self.generated_code);
        dispatch_generator::generate_dispatch(interface_def, self.generated_code);
    }

    fn visit_enum(&mut self, enum_def: &Enum) {
        enum_generator::generate_enum(enum_def, self.generated_code);
    }

    fn visit_operation(&mut self, _: &Operation) {}

    fn visit_custom_type(&mut self, _: &CustomType) {}

    fn visit_type_alias(&mut self, _: &TypeAlias) {}

    fn visit_field(&mut self, _: &Field) {}

    fn visit_parameter(&mut self, _: &Parameter) {}

    fn visit_enumerator(&mut self, _: &Enumerator) {}
}

pub fn generate_from_slice_file(slice_file: &SliceFile) -> String {
    let mut generated_code = GeneratedCode::new();

    generated_code.preamble.push(preamble(slice_file));

    let mut generator = Generator {
        generated_code: &mut generated_code,
    };
    slice_file.visit_with(&mut generator);

    namespace_generator::generate_namespaces(slice_file, &mut generated_code);

    // Move the generated code out of the generated_code struct and consolidate into a
    // single string.
    generated_code
        .preamble
        .into_iter()
        .chain(generated_code.code_blocks.into_iter())
        .collect::<CodeBlock>()
        .to_string()
        + "\n" // End the file with a trailing newline.
}

fn preamble(slice_file: &SliceFile) -> CodeBlock {
    format!(
        r#"// <auto-generated/>
// slicec-cs version: '{version}'
// Generated from file: '{file}.slice'

#nullable enable

#pragma warning disable 1591 // Missing XML Comment
#pragma warning disable 1573 // Parameter has no matching param tag in the XML comment

using IceRpc.Slice;

[assembly:Slice("{file}.slice")]"#,
        version = env!("CARGO_PKG_VERSION"),
        file = slice_file.filename,
    )
    .into()
}
